using BibleMemorization.Core.Model;

namespace BibleMemorization.Core.Storage;

/// <summary>
/// Holds a library for the lifetime of the page. Backs demo mode and the tests,
/// where nothing should persist between runs.
/// </summary>
public sealed class InMemoryStorageProvider : IIncrementalStorageProvider
{
    public const string ProviderId = "memory";

    private LibrarySnapshot _snapshot;

    public InMemoryStorageProvider(LibrarySnapshot? seed = null)
    {
        _snapshot = seed ?? LibrarySnapshot.Empty;
    }

    public string Id => ProviderId;

    public string DisplayName => "In memory (not saved)";

    public StorageCapabilities Capabilities =>
        StorageCapabilities.ReadWrite | StorageCapabilities.AutoSave;

    public Task<LibrarySnapshot> LoadAsync(CancellationToken ct = default) =>
        Task.FromResult(_snapshot);

    public Task SaveAsync(LibrarySnapshot snapshot, CancellationToken ct = default)
    {
        _snapshot = snapshot;
        return Task.CompletedTask;
    }

    public Task UpsertPassageAsync(Passage passage, CancellationToken ct = default)
    {
        var passages = _snapshot.Passages.Where(p => p.Id != passage.Id).Append(passage).ToArray();
        _snapshot = _snapshot with { Passages = passages };
        return Task.CompletedTask;
    }

    public Task DeletePassageAsync(Guid passageId, CancellationToken ct = default)
    {
        _snapshot = _snapshot with
        {
            Passages = _snapshot.Passages.Where(p => p.Id != passageId).ToArray(),
            // Progress is meaningless without its passage, so it goes too.
            Progress = _snapshot.Progress.Where(p => p.PassageId != passageId).ToArray(),
        };

        return Task.CompletedTask;
    }

    public Task UpsertProgressAsync(PracticeProgress progress, CancellationToken ct = default)
    {
        var others = _snapshot.Progress.Where(p => p.Key != progress.Key);
        _snapshot = _snapshot with { Progress = others.Append(progress).ToArray() };
        return Task.CompletedTask;
    }

    public Task SaveSettingsAsync(AppSettings settings, CancellationToken ct = default)
    {
        _snapshot = _snapshot with { Settings = settings };
        return Task.CompletedTask;
    }
}
