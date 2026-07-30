using BibleMemorization.Core.Model;

namespace BibleMemorization.Core.Storage;

[Flags]
public enum StorageCapabilities
{
    None = 0,

    Read = 1 << 0,

    Write = 1 << 1,

    /// <summary>
    /// Safe to write to in the background on every change. False for the save file,
    /// which would mean a download per keystroke.
    /// </summary>
    AutoSave = 1 << 2,

    /// <summary>
    /// Needs a click to work: browsers only allow downloads and file pickers from a
    /// user gesture. The UI uses this to decide between a background sync and an
    /// explicit Export/Import button.
    /// </summary>
    RequiresUserGesture = 1 << 3,

    /// <summary>Needs credentials before it can be used.</summary>
    RequiresAuth = 1 << 4,

    ReadWrite = Read | Write,
}

/// <summary>
/// Somewhere a library can live.
///
/// Browser storage, the .save file, and a remote API are all implementations of
/// this one interface, so calling code never learns where the data went. Adding a
/// database backend later means writing this interface and registering it.
/// </summary>
public interface IStorageProvider
{
    /// <summary>Stable id, persisted in settings. Never change it for a shipped provider.</summary>
    string Id { get; }

    string DisplayName { get; }

    StorageCapabilities Capabilities { get; }

    /// <summary>
    /// Reads the whole library. Returns <see cref="LibrarySnapshot.Empty"/> when the
    /// backing store holds nothing yet — a first run is not an error.
    /// </summary>
    Task<LibrarySnapshot> LoadAsync(CancellationToken ct = default);

    Task SaveAsync(LibrarySnapshot snapshot, CancellationToken ct = default);
}

/// <summary>
/// Implemented by backends that can write one record at a time.
///
/// Whole-snapshot writes are fine for localStorage but wrong for a network
/// backend, where saving the entire library on every keystroke would be absurd.
/// <c>LibraryService</c> uses these methods when a provider offers them and falls
/// back to <see cref="IStorageProvider.SaveAsync"/> when it does not.
/// </summary>
public interface IIncrementalStorageProvider : IStorageProvider
{
    Task UpsertPassageAsync(Passage passage, CancellationToken ct = default);

    Task DeletePassageAsync(Guid passageId, CancellationToken ct = default);

    Task UpsertProgressAsync(PracticeProgress progress, CancellationToken ct = default);

    Task SaveSettingsAsync(AppSettings settings, CancellationToken ct = default);
}
