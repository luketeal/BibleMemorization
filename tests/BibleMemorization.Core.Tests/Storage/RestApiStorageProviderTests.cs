using System.Net;
using BibleMemorization.Core.Model;
using BibleMemorization.Core.Storage;

namespace BibleMemorization.Core.Tests.Storage;

/// <summary>
/// Proves IStorageProvider genuinely supports a remote backend, not just
/// localStorage. Runs entirely against a stubbed handler — no server required.
/// </summary>
public class RestApiStorageProviderTests
{
    private static readonly Guid PassageId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static (RestApiStorageProvider Provider, StubHttpMessageHandler Handler) Create()
    {
        var handler = new StubHttpMessageHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/api/") };
        return (new RestApiStorageProvider(client), handler);
    }

    private static Passage SamplePassage() => new()
    {
        Id = PassageId,
        Title = "Psalm 23:1",
        Text = "The LORD is my shepherd",
        Source = PassageSource.BibleApi,
        CreatedUtc = DateTimeOffset.UnixEpoch,
        ModifiedUtc = DateTimeOffset.UnixEpoch,
    };

    [Fact]
    public void It_declares_itself_as_a_network_backend()
    {
        var (provider, _) = Create();

        Assert.True(provider.Capabilities.HasFlag(StorageCapabilities.AutoSave));
        Assert.True(provider.Capabilities.HasFlag(StorageCapabilities.RequiresAuth));
        Assert.False(provider.Capabilities.HasFlag(StorageCapabilities.RequiresUserGesture));
    }

    [Fact]
    public async Task Loading_reads_the_library_from_the_server()
    {
        var (provider, handler) = Create();
        var snapshot = new LibrarySnapshot { Passages = [SamplePassage()] };
        handler.Respond(HttpMethod.Get, "api/library", HttpStatusCode.OK, SnapshotSerializer.Serialize(snapshot));

        var loaded = await provider.LoadAsync();

        var passage = Assert.Single(loaded.Passages);
        Assert.Equal("Psalm 23:1", passage.Title);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.NoContent)]
    public async Task An_empty_backend_is_a_first_run_not_a_failure(HttpStatusCode status)
    {
        var (provider, handler) = Create();
        handler.Respond(HttpMethod.Get, "api/library", status);

        var loaded = await provider.LoadAsync();

        Assert.True(loaded.IsEmpty);
    }

    [Fact]
    public async Task A_server_error_surfaces_rather_than_silently_losing_data()
    {
        var (provider, handler) = Create();
        handler.Respond(HttpMethod.Get, "api/library", HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<HttpRequestException>(() => provider.LoadAsync());
    }

    [Fact]
    public async Task Saving_one_passage_sends_only_that_passage()
    {
        var (provider, handler) = Create();

        await provider.UpsertPassageAsync(SamplePassage());

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal($"api/passages/{PassageId:N}", request.Path);
        Assert.Contains("Psalm 23:1", request.Body);
        // The point of the incremental interface: no whole-library payload.
        Assert.DoesNotContain("\"passages\"", request.Body);
    }

    [Fact]
    public async Task Deleting_a_passage_issues_a_delete()
    {
        var (provider, handler) = Create();

        await provider.DeletePassageAsync(PassageId);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal($"api/passages/{PassageId:N}", request.Path);
    }

    [Fact]
    public async Task Deleting_something_already_gone_is_not_an_error()
    {
        var (provider, handler) = Create();
        handler.Respond(HttpMethod.Delete, $"api/passages/{PassageId:N}", HttpStatusCode.NotFound);

        // The caller wanted it absent, and it is absent.
        await provider.DeletePassageAsync(PassageId);
    }

    [Fact]
    public async Task Progress_is_saved_under_its_composite_key()
    {
        var (provider, handler) = Create();
        var progress = new PracticeProgress
        {
            PassageId = PassageId,
            TechniqueId = "vanishing-text",
            HiddenTokenIndices = [1, 2],
        };

        await provider.UpsertProgressAsync(progress);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Contains(Uri.EscapeDataString(progress.Key), request.Path);
    }

    [Fact]
    public async Task Settings_are_saved_on_their_own_endpoint()
    {
        var (provider, handler) = Create();

        await provider.SaveSettingsAsync(AppSettings.Default with { SpeechRate = 1.5 });

        var request = Assert.Single(handler.Requests);
        Assert.Equal("api/settings", request.Path);
        Assert.Contains("1.5", request.Body);
    }

    [Fact]
    public async Task A_whole_library_save_round_trips_through_the_server()
    {
        var (provider, handler) = Create();
        var snapshot = new LibrarySnapshot { Passages = [SamplePassage()] };

        await provider.SaveAsync(snapshot);

        var body = Assert.Single(handler.Requests).Body;
        Assert.Equal(
            SnapshotSerializer.Serialize(snapshot),
            SnapshotSerializer.Serialize(SnapshotSerializer.Deserialize(body)));
    }
}
