using BibleMemorization.Core.Model;
using BibleMemorization.Core.Storage;

namespace BibleMemorization.Core.Tests.Storage;

/// <summary>
/// What happens when the stored library is not the tidy thing the app wrote.
///
/// This matters more than it looks: the load runs before the first render, so an
/// exception escaping here is not an error message — it is a permanently blank page,
/// with the offending data still in localStorage to do it again on every refresh.
/// </summary>
public class SaveFileResilienceTests
{
    /// <summary>
    /// Source-generated deserialization does not run property initializers, so a file
    /// that is entirely valid JSON and passes every version check still comes back
    /// with null collections. Measured, not assumed.
    /// </summary>
    [Fact]
    public void Absent_properties_come_back_as_usable_collections()
    {
        var snapshot = SnapshotSerializer.Deserialize("""{"schemaVersion":1}""");

        Assert.NotNull(snapshot.Passages);
        Assert.NotNull(snapshot.Progress);
        Assert.NotNull(snapshot.Settings);
        Assert.True(snapshot.IsEmpty);
    }

    [Fact]
    public void Explicit_nulls_come_back_as_usable_collections()
    {
        var snapshot = SnapshotSerializer.Deserialize(
            """{"schemaVersion":1,"passages":null,"progress":null,"settings":null}""");

        Assert.Empty(snapshot.Passages);
        Assert.Empty(snapshot.Progress);
        Assert.Equal(AppSettings.Default, snapshot.Settings);
    }

    /// <summary>
    /// The property everything downstream relies on: whatever comes out of Deserialize
    /// can be walked without a null check.
    /// </summary>
    [Fact]
    public async Task A_minimal_file_loads_into_a_working_library()
    {
        var store = new InMemoryStorageProvider(SnapshotSerializer.Deserialize("""{"schemaVersion":1}"""));
        var library = new LibraryService(new StorageProviderRegistry([store]), FixedClock.AtDefault());

        await library.InitializeAsync();

        Assert.Empty(library.Passages);
        Assert.Equal(AppSettings.Default, library.Settings);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("\"a string\"")]
    [InlineData("""{"schemaVersion":1,"passages":"not a list"}""")]
    [InlineData("""{"schemaVersion":1,"passages":[{"id":"not-a-guid"}]}""")]
    public void Garbage_is_rejected_as_a_format_problem_rather_than_anything_else(string json)
    {
        // Every failure mode has to arrive as this one type, because that is the only
        // thing the import UI knows how to explain to the user.
        Assert.Throws<SaveFileFormatException>(() => SnapshotSerializer.Deserialize(json));
    }

    [Fact]
    public void An_empty_file_reads_as_an_empty_library()
    {
        Assert.True(SnapshotSerializer.Deserialize("").IsEmpty);
        Assert.True(SnapshotSerializer.Deserialize("   ").IsEmpty);
        Assert.True(SnapshotSerializer.Deserialize(null).IsEmpty);
    }

    [Fact]
    public void A_file_from_a_newer_app_is_refused_by_name()
    {
        var error = Assert.Throws<SaveFileFormatException>(
            () => SnapshotSerializer.Deserialize("""{"schemaVersion":99}"""));

        Assert.Contains("99", error.Message);
    }

    /// <summary>
    /// The README invites people to open a .save file, so computed properties leaking
    /// into it are not just bytes — they are noise that reads like data you could edit.
    /// </summary>
    [Fact]
    public void Computed_properties_stay_out_of_the_file()
    {
        var snapshot = new LibrarySnapshot
        {
            Passages = [new Passage { Id = Guid.NewGuid(), Title = "John 3:16", Text = "For God so loved" }],
            Progress =
            [
                new PracticeProgress
                {
                    PassageId = Guid.NewGuid(),
                    TechniqueId = "vanishing-text",
                    HiddenTokenIndices = [1],
                },
            ],
        };

        var json = SnapshotSerializer.Serialize(snapshot);

        Assert.DoesNotContain("isEmpty", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"key\"", json, StringComparison.OrdinalIgnoreCase);

        // Still a full round trip, so nothing real was dropped along with them.
        var reread = SnapshotSerializer.Deserialize(json);
        Assert.Equal("John 3:16", Assert.Single(reread.Passages).Title);
        Assert.Equal([1], Assert.Single(reread.Progress).HiddenTokenIndices);
    }
}
