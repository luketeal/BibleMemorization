using BibleMemorization.Core;
using BibleMemorization.Core.Model;
using BibleMemorization.Core.Storage;

namespace BibleMemorization.Core.Tests.Storage;

public class LibraryServiceTests
{
    private static (LibraryService Library, InMemoryStorageProvider Store, FixedClock Clock) Create(
        LibrarySnapshot? seed = null)
    {
        var store = new InMemoryStorageProvider(seed);
        var clock = FixedClock.AtDefault();
        return (new LibraryService(new StorageProviderRegistry([store]), clock), store, clock);
    }

    [Fact]
    public async Task Adding_a_passage_stores_it_and_stamps_the_clock()
    {
        var (library, store, clock) = Create();
        await library.InitializeAsync();

        var passage = await library.AddPassageAsync("My verse", "For God so loved", PassageSource.Written);

        Assert.Equal(clock.UtcNow, passage.CreatedUtc);
        Assert.Single(library.Passages);
        Assert.Single((await store.LoadAsync()).Passages);
    }

    [Fact]
    public async Task A_saved_library_reloads_from_storage()
    {
        var (library, store, _) = Create();
        await library.InitializeAsync();
        await library.AddPassageAsync("Psalm 23", "The LORD is my shepherd", PassageSource.BibleApi);

        var reopened = new LibraryService(new StorageProviderRegistry([store]), FixedClock.AtDefault());
        await reopened.InitializeAsync();

        Assert.Single(reopened.Passages);
        Assert.Equal("Psalm 23", reopened.Passages[0].Title);
    }

    [Fact]
    public async Task Passages_are_listed_most_recently_changed_first()
    {
        var (library, _, clock) = Create();
        await library.InitializeAsync();

        var first = await library.AddPassageAsync("First", "one", PassageSource.Written);
        clock.Advance(TimeSpan.FromMinutes(5));
        await library.AddPassageAsync("Second", "two", PassageSource.Written);
        clock.Advance(TimeSpan.FromMinutes(5));
        await library.UpdatePassageAsync(first with { Title = "First, edited" });

        Assert.Equal(["First, edited", "Second"], library.Passages.Select(p => p.Title));
    }

    [Fact]
    public async Task Deleting_a_passage_also_removes_its_progress()
    {
        var (library, _, _) = Create();
        await library.InitializeAsync();
        var passage = await library.AddPassageAsync("Verse", "For God so loved", PassageSource.Written);

        await library.SaveProgressAsync(new PracticeProgress
        {
            PassageId = passage.Id,
            TechniqueId = "vanishing-text",
            HiddenTokenIndices = [1],
        });

        await library.DeletePassageAsync(passage.Id);

        Assert.Empty(library.Passages);
        // Orphaned progress would be unreachable but still consume the storage budget.
        Assert.Empty(library.ToSnapshot().Progress);
    }

    [Fact]
    public async Task Progress_is_kept_separately_per_technique()
    {
        var (library, _, _) = Create();
        await library.InitializeAsync();
        var passage = await library.AddPassageAsync("Verse", "For God so loved", PassageSource.Written);

        await library.SaveProgressAsync(new PracticeProgress
        {
            PassageId = passage.Id,
            TechniqueId = "vanishing-text",
            HiddenTokenIndices = [1, 2],
        });

        // Switching technique must not disturb what was hidden under the other one.
        Assert.Equal([1, 2], library.GetProgress(passage.Id, "vanishing-text").HiddenTokenIndices);
        Assert.Empty(library.GetProgress(passage.Id, "first-letter").HiddenTokenIndices);
    }

    [Fact]
    public async Task Progress_for_an_untouched_passage_comes_back_empty_rather_than_null()
    {
        var (library, _, _) = Create();
        await library.InitializeAsync();

        var progress = library.GetProgress(Guid.NewGuid(), "vanishing-text");

        Assert.Empty(progress.HiddenTokenIndices);
        Assert.Empty(progress.Attempts);
    }

    [Fact]
    public async Task Clearing_removes_everything_including_settings()
    {
        var (library, _, _) = Create();
        await library.InitializeAsync();
        await library.AddPassageAsync("Verse", "text", PassageSource.Written);
        await library.SaveSettingsAsync(AppSettings.Default with { SpeechRate = 2.0 });

        await library.ClearAsync();

        Assert.Empty(library.Passages);
        Assert.Equal(AppSettings.Default, library.Settings);
    }

    [Fact]
    public async Task Changes_raise_an_event_so_the_ui_can_refresh()
    {
        var (library, _, _) = Create();
        await library.InitializeAsync();

        var count = 0;
        library.Changed += () => count++;

        await library.AddPassageAsync("Verse", "text", PassageSource.Written);
        await library.SaveSettingsAsync(AppSettings.Default);

        Assert.Equal(2, count);
    }

    // ---- Export and import ----

    [Fact]
    public async Task Exporting_hands_the_whole_library_to_another_provider()
    {
        var (library, _, _) = Create();
        await library.InitializeAsync();
        await library.AddPassageAsync("Verse", "For God so loved", PassageSource.Written);

        var file = new InMemoryStorageProvider();
        await library.ExportToAsync(file);

        Assert.Single((await file.LoadAsync()).Passages);
    }

    [Fact]
    public async Task Replacing_on_import_discards_what_was_there()
    {
        var (library, _, _) = Create();
        await library.InitializeAsync();
        await library.AddPassageAsync("Existing", "old text", PassageSource.Written);

        var incoming = new LibrarySnapshot { Passages = [NewPassage("Imported", DateTimeOffset.UnixEpoch)] };
        var result = await library.ImportAsync(incoming, ImportMode.Replace);

        Assert.Equal(ImportMode.Replace, result.Mode);
        Assert.Equal(["Imported"], library.Passages.Select(p => p.Title));
    }

    [Fact]
    public async Task Merging_on_import_keeps_both_libraries()
    {
        var (library, _, _) = Create();
        await library.InitializeAsync();
        await library.AddPassageAsync("Existing", "old text", PassageSource.Written);

        var incoming = new LibrarySnapshot { Passages = [NewPassage("Imported", DateTimeOffset.UnixEpoch)] };
        var result = await library.ImportAsync(incoming, ImportMode.Merge);

        Assert.Equal(1, result.Added);
        Assert.Equal(2, library.Passages.Count);
    }

    /// <summary>
    /// The reason merge compares timestamps: importing an old backup must not roll
    /// back edits the user has made since.
    /// </summary>
    [Fact]
    public async Task Merging_an_older_copy_does_not_overwrite_newer_work()
    {
        var (library, _, clock) = Create();
        await library.InitializeAsync();
        var passage = await library.AddPassageAsync("Verse", "current text", PassageSource.Written);

        clock.Advance(TimeSpan.FromDays(1));
        await library.UpdatePassageAsync(passage with { Text = "text edited today" });

        var staleBackup = new LibrarySnapshot
        {
            Passages = [passage with { Text = "text from last week", ModifiedUtc = DateTimeOffset.UnixEpoch }],
        };

        var result = await library.ImportAsync(staleBackup, ImportMode.Merge);

        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Updated);
        Assert.Equal("text edited today", library.Passages[0].Text);
    }

    [Fact]
    public async Task Merging_a_newer_copy_does_update()
    {
        var (library, _, clock) = Create();
        await library.InitializeAsync();
        var passage = await library.AddPassageAsync("Verse", "old text", PassageSource.Written);

        var newer = new LibrarySnapshot
        {
            Passages = [passage with { Text = "newer text", ModifiedUtc = clock.UtcNow.AddDays(1) }],
        };

        var result = await library.ImportAsync(newer, ImportMode.Merge);

        Assert.Equal(1, result.Updated);
        Assert.Equal("newer text", library.Passages[0].Text);
    }

    [Fact]
    public async Task An_imported_library_survives_a_reload()
    {
        var (library, store, _) = Create();
        await library.InitializeAsync();

        await library.ImportAsync(
            new LibrarySnapshot { Passages = [NewPassage("Imported", DateTimeOffset.UnixEpoch)] },
            ImportMode.Merge);

        var reopened = new LibraryService(new StorageProviderRegistry([store]), FixedClock.AtDefault());
        await reopened.InitializeAsync();

        Assert.Single(reopened.Passages);
    }

    private static Passage NewPassage(string title, DateTimeOffset modified) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Text = "some text",
        Source = PassageSource.Written,
        CreatedUtc = modified,
        ModifiedUtc = modified,
    };
}
