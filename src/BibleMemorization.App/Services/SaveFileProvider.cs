using BibleMemorization.Core.Storage;
using Microsoft.JSInterop;

namespace BibleMemorization.App.Services;

/// <summary>
/// Reads and writes the portable .save file.
///
/// Saving downloads a file and loading reads one the user picked, so both need a
/// click — hence RequiresUserGesture and no AutoSave. The registry uses those flags
/// to refuse making this the live backend, which would mean a download per
/// keystroke.
/// </summary>
public sealed class SaveFileProvider(IJSRuntime jsRuntime) : IStorageProvider
{
    public const string ProviderId = "savefile";

    public const string FileExtension = ".save";

    private readonly JsModule _module = new(jsRuntime, "./js/storage.js");

    /// <summary>Set by the UI to the file the user picked, just before LoadAsync.</summary>
    public string? PendingImportJson { get; set; }

    public string Id => ProviderId;

    public string DisplayName => "Save file";

    public StorageCapabilities Capabilities =>
        StorageCapabilities.ReadWrite | StorageCapabilities.RequiresUserGesture;

    public Task<LibrarySnapshot> LoadAsync(CancellationToken ct = default)
    {
        if (PendingImportJson is null)
        {
            throw new InvalidOperationException(
                "No file has been chosen. Set PendingImportJson from the file picker first.");
        }

        var snapshot = SnapshotSerializer.Deserialize(PendingImportJson);
        PendingImportJson = null;
        return Task.FromResult(snapshot);
    }

    public async Task SaveAsync(LibrarySnapshot snapshot, CancellationToken ct = default)
    {
        var json = SnapshotSerializer.Serialize(snapshot);
        await _module.InvokeVoidAsync("downloadText", SuggestFileName(snapshot), json, "application/json");
    }

    public static string SuggestFileName(LibrarySnapshot snapshot) =>
        $"biblememory-{snapshot.ExportedUtc:yyyy-MM-dd}{FileExtension}";
}
