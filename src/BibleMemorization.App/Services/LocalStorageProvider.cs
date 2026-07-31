using BibleMemorization.Core.Storage;
using BibleMemorization.Core.Model;
using Microsoft.JSInterop;

namespace BibleMemorization.App.Services;

/// <summary>
/// Keeps the library in the browser's localStorage.
///
/// Writes the whole snapshot rather than implementing the incremental interface:
/// localStorage has no partial-write API, so a per-record path would serialize the
/// same string anyway. Libraries of text are small enough that this is cheap.
/// </summary>
public sealed class LocalStorageProvider(IJSRuntime jsRuntime) : IStorageProvider
{
    public const string ProviderId = "localstorage";

    private const string StorageKey = "biblememorization.library";

    private readonly JsModule _module = new(jsRuntime, "./js/storage.js");

    public string Id => ProviderId;

    public string DisplayName => "This browser";

    public StorageCapabilities Capabilities =>
        StorageCapabilities.ReadWrite | StorageCapabilities.AutoSave;

    /// <summary>Set when a write is refused, e.g. quota exceeded or storage blocked.</summary>
    public string? LastError { get; private set; }

    public async Task<LibrarySnapshot> LoadAsync(CancellationToken ct = default)
    {
        var json = await _module.InvokeAsync<string?>("get", StorageKey);

        try
        {
            return SnapshotSerializer.Deserialize(json);
        }
        catch (Exception ex)
        {
            // Deliberately every exception, not just SaveFileFormatException. This
            // runs before the first render, so anything that escapes here is a blank
            // page with no way back — which is exactly what the promise below rules
            // out. Corrupt local data must not lock the user out of the app entirely,
            // so start empty and say so, leaving the bad value in place for recovery.
            LastError = $"Saved data in this browser could not be read ({ex.Message}). Starting empty.";
            return LibrarySnapshot.Empty;
        }
    }

    public async Task SaveAsync(LibrarySnapshot snapshot, CancellationToken ct = default)
    {
        var json = SnapshotSerializer.Serialize(snapshot, pretty: false);
        var stored = await _module.InvokeAsync<bool>("set", StorageKey, json);

        LastError = stored
            ? null
            : "This browser refused to save. Your work is still here, but export it to a file to keep it.";
    }

    public ValueTask<bool> IsAvailableAsync() => _module.InvokeAsync<bool>("isAvailable");
}
