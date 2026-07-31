using BibleMemorization.Core.Model;
using BibleMemorization.Core.Storage;

namespace BibleMemorization.Core.Tests.Storage;

/// <summary>
/// Merge import claims to never roll work back. Passages were guarded by
/// <c>ModifiedUtc</c>, but progress was copied over unconditionally — so importing
/// an old backup correctly skipped the stale passage text while silently wiping the
/// hidden words and attempt history the user had built since. Same file, same click,
/// no warning.
/// </summary>
public class MergeImportProgressTests
{
    private static readonly Guid PassageId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private const string Technique = "vanishing-text";

    private static (LibraryService Library, FixedClock Clock) Create()
    {
        var clock = FixedClock.AtDefault();
        var store = new InMemoryStorageProvider();
        return (new LibraryService(new StorageProviderRegistry([store]), clock), clock);
    }

    private static PracticeProgress Progress(int[] hidden, int attempts, DateTimeOffset updated) => new()
    {
        PassageId = PassageId,
        TechniqueId = Technique,
        HiddenTokenIndices = hidden,
        UpdatedUtc = updated,
        Attempts = [.. Enumerable.Range(0, attempts).Select(_ => new Attempt
        {
            Utc = updated,
            TechniqueId = Technique,
            Accuracy = 1,
        })],
    };

    private static LibrarySnapshot Backup(PracticeProgress progress, DateTimeOffset passageModified) => new()
    {
        Passages =
        [
            new Passage
            {
                Id = PassageId,
                Title = "John 3:16",
                Text = "For God so loved the world",
                ModifiedUtc = passageModified,
            },
        ],
        Progress = [progress],
    };

    private static async Task<LibraryService> WithLocalWork(FixedClock clock, LibraryService library)
    {
        await library.InitializeAsync();

        await library.UpdatePassageAsync(new Passage
        {
            Id = PassageId,
            Title = "John 3:16",
            Text = "For God so loved the world",
        });

        await library.SaveProgressAsync(Progress([1, 3, 5], attempts: 2, clock.UtcNow));
        return library;
    }

    [Fact]
    public async Task An_older_backup_leaves_current_progress_alone()
    {
        var (library, clock) = Create();
        await WithLocalWork(clock, library);

        var lastYear = clock.UtcNow.AddYears(-1);
        await library.ImportAsync(Backup(Progress([], 0, lastYear), lastYear), ImportMode.Merge);

        var progress = library.GetProgress(PassageId, Technique);
        Assert.Equal([1, 3, 5], progress.HiddenTokenIndices);
        Assert.Equal(2, progress.Attempts.Count);
    }

    [Fact]
    public async Task A_newer_backup_does_replace_current_progress()
    {
        var (library, clock) = Create();
        await WithLocalWork(clock, library);

        var tomorrow = clock.UtcNow.AddDays(1);
        await library.ImportAsync(Backup(Progress([2, 4], 7, tomorrow), tomorrow), ImportMode.Merge);

        var progress = library.GetProgress(PassageId, Technique);
        Assert.Equal([2, 4], progress.HiddenTokenIndices);
        Assert.Equal(7, progress.Attempts.Count);
    }

    /// <summary>
    /// Files written before UpdatedUtc existed have no value for it, so it lands at
    /// MinValue. That is the safe default in exactly the right direction: a legacy
    /// backup loses every tie and can never overwrite work done since.
    /// </summary>
    [Fact]
    public async Task Progress_from_a_file_predating_the_timestamp_never_wins()
    {
        var (library, clock) = Create();
        await WithLocalWork(clock, library);

        var legacy = SnapshotSerializer.Deserialize($$"""
            {
              "schemaVersion": 1,
              "passages": [],
              "progress": [
                { "passageId": "{{PassageId}}", "techniqueId": "{{Technique}}", "hiddenTokenIndices": [] }
              ]
            }
            """);

        Assert.Equal(DateTimeOffset.MinValue, Assert.Single(legacy.Progress).UpdatedUtc);

        await library.ImportAsync(legacy, ImportMode.Merge);

        Assert.Equal([1, 3, 5], library.GetProgress(PassageId, Technique).HiddenTokenIndices);
    }

    [Fact]
    public async Task Progress_for_a_passage_not_held_locally_is_brought_in()
    {
        var (library, clock) = Create();
        await WithLocalWork(clock, library);

        var other = Guid.Parse("22222222-2222-2222-2222-222222222222");
        await library.ImportAsync(
            new LibrarySnapshot
            {
                Passages = [new Passage { Id = other, Title = "Psalm 23", Text = "The LORD is my shepherd" }],
                Progress =
                [
                    new PracticeProgress
                    {
                        PassageId = other,
                        TechniqueId = Technique,
                        HiddenTokenIndices = [0],
                    },
                ],
            },
            ImportMode.Merge);

        // Guarding the merge must not turn into refusing genuinely new work.
        Assert.Equal([0], library.GetProgress(other, Technique).HiddenTokenIndices);
    }

    [Fact]
    public async Task Replace_import_takes_the_file_as_given()
    {
        var (library, clock) = Create();
        await WithLocalWork(clock, library);

        var lastYear = clock.UtcNow.AddYears(-1);
        await library.ImportAsync(Backup(Progress([], 0, lastYear), lastYear), ImportMode.Replace);

        // Replace is the user explicitly asking for the file to win, timestamps and all.
        Assert.Empty(library.GetProgress(PassageId, Technique).HiddenTokenIndices);
    }

    [Fact]
    public async Task Saving_progress_stamps_the_time_so_later_imports_can_compare()
    {
        var (library, clock) = Create();
        await library.InitializeAsync();

        clock.Advance(TimeSpan.FromHours(3));
        await library.SaveProgressAsync(new PracticeProgress
        {
            PassageId = PassageId,
            TechniqueId = Technique,
        });

        // Stamped centrally rather than by callers, so every write is comparable.
        Assert.Equal(clock.UtcNow, library.GetProgress(PassageId, Technique).UpdatedUtc);
    }

    /// <summary>
    /// Attempts were appended for ever while only the last few are ever shown, so a
    /// well-practised passage grew towards the localStorage quota storing history
    /// nobody reads.
    /// </summary>
    [Fact]
    public async Task Attempt_history_is_capped_keeping_the_most_recent()
    {
        var (library, clock) = Create();
        await library.InitializeAsync();

        var attempts = Enumerable.Range(0, 200)
            .Select(i => new Attempt
            {
                Utc = clock.UtcNow.AddMinutes(i),
                TechniqueId = Technique,
                Accuracy = i / 200.0,
            })
            .ToArray();

        await library.SaveProgressAsync(new PracticeProgress
        {
            PassageId = PassageId,
            TechniqueId = Technique,
            Attempts = attempts,
        });

        var stored = library.GetProgress(PassageId, Technique).Attempts;
        Assert.Equal(50, stored.Count);
        Assert.Equal(attempts[^1].Utc, stored[^1].Utc);
        Assert.Equal(attempts[^50].Utc, stored[0].Utc);
    }

    [Fact]
    public async Task A_short_history_is_left_exactly_as_it_was()
    {
        var (library, clock) = Create();
        await library.InitializeAsync();

        await library.SaveProgressAsync(new PracticeProgress
        {
            PassageId = PassageId,
            TechniqueId = Technique,
            Attempts = [new Attempt { Utc = clock.UtcNow, TechniqueId = Technique, Accuracy = 0.5 }],
        });

        Assert.Single(library.GetProgress(PassageId, Technique).Attempts);
    }
}
