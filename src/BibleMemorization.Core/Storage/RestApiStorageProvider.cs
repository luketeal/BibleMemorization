using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using BibleMemorization.Core.Model;

namespace BibleMemorization.Core.Storage;

/// <summary>
/// Stores the library behind a plain REST API.
///
/// This exists to keep <see cref="IStorageProvider"/> honest. An abstraction that
/// has only ever been implemented against localStorage tends to quietly assume
/// local semantics — synchronous, infallible, cheap to rewrite wholesale. Building
/// a network backend against the same interface proves those assumptions are not
/// baked in, and it is covered by tests against a stubbed handler so no live server
/// is needed.
///
/// Contract:
///   GET    {base}/library            -> LibrarySnapshot
///   PUT    {base}/library            &lt;- LibrarySnapshot
///   PUT    {base}/passages/{id}      &lt;- Passage
///   DELETE {base}/passages/{id}
///   PUT    {base}/progress/{key}     &lt;- PracticeProgress
///   PUT    {base}/settings           &lt;- AppSettings
/// </summary>
public sealed class RestApiStorageProvider(HttpClient httpClient) : IIncrementalStorageProvider
{
    public const string ProviderId = "rest";

    public string Id => ProviderId;

    public string DisplayName => "Remote API";

    public StorageCapabilities Capabilities =>
        StorageCapabilities.ReadWrite | StorageCapabilities.AutoSave | StorageCapabilities.RequiresAuth;

    public async Task<LibrarySnapshot> LoadAsync(CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync("library", ct);

        // A backend with nothing stored yet is a first run, not a failure.
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
        {
            return LibrarySnapshot.Empty;
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return SnapshotSerializer.Deserialize(json);
    }

    public async Task SaveAsync(LibrarySnapshot snapshot, CancellationToken ct = default)
    {
        using var content = JsonContent(snapshot, LibraryJsonContext.Compact.LibrarySnapshot);
        var response = await httpClient.PutAsync("library", content, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpsertPassageAsync(Passage passage, CancellationToken ct = default)
    {
        using var content = JsonContent(passage, LibraryJsonContext.Compact.Passage);
        var response = await httpClient.PutAsync($"passages/{passage.Id:N}", content, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeletePassageAsync(Guid passageId, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync($"passages/{passageId:N}", ct);

        // Deleting something already gone is the outcome the caller wanted.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    public async Task UpsertProgressAsync(PracticeProgress progress, CancellationToken ct = default)
    {
        using var content = JsonContent(progress, LibraryJsonContext.Compact.PracticeProgress);
        var response = await httpClient.PutAsync($"progress/{Uri.EscapeDataString(progress.Key)}", content, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken ct = default)
    {
        using var content = JsonContent(settings, LibraryJsonContext.Compact.AppSettings);
        var response = await httpClient.PutAsync("settings", content, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Serializes through the source-generated context rather than
    /// <c>JsonContent.Create</c>, which would take the reflection path and break
    /// under trimming.
    /// </summary>
    private static StringContent JsonContent<T>(T value, JsonTypeInfo<T> typeInfo) =>
        new(JsonSerializer.Serialize(value, typeInfo), Encoding.UTF8, "application/json");
}
