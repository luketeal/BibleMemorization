using BibleMemorization.Core.Model;

namespace BibleMemorization.Core.Storage;

/// <summary>
/// The app's view of the library: everything in memory, written through to whichever
/// storage provider is active.
///
/// Callers never talk to a provider directly, which is what lets the backing store
/// change from localStorage to a REST API without touching a single page.
/// </summary>
public sealed class LibraryService(StorageProviderRegistry providers, IClock clock)
{
    /// <summary>Enough for a meaningful history; the UI shows the last five.</summary>
    private const int MaxStoredAttempts = 50;

    private readonly List<Passage> _passages = [];
    private readonly Dictionary<string, PracticeProgress> _progress = [];

    /// <summary>Raised after any change, so the UI can re-render.</summary>
    public event Action? Changed;

    public IReadOnlyList<Passage> Passages =>
        _passages.OrderByDescending(p => p.ModifiedUtc).ToArray();

    public AppSettings Settings { get; private set; } = AppSettings.Default;

    public bool IsLoaded { get; private set; }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var snapshot = await providers.Active.LoadAsync(ct);
        Apply(snapshot);
        IsLoaded = true;
        Changed?.Invoke();
    }

    public Passage? FindPassage(Guid id) => _passages.FirstOrDefault(p => p.Id == id);

    public PracticeProgress GetProgress(Guid passageId, string techniqueId)
    {
        var key = PracticeProgress.KeyFor(passageId, techniqueId);

        return _progress.TryGetValue(key, out var progress)
            ? progress
            : new PracticeProgress { PassageId = passageId, TechniqueId = techniqueId };
    }

    public async Task<Passage> AddPassageAsync(
        string title,
        string text,
        PassageSource source,
        string reference = "",
        string translation = "",
        string translationName = "",
        CancellationToken ct = default)
    {
        var now = clock.UtcNow;

        var passage = new Passage
        {
            Id = Guid.NewGuid(),
            Title = title,
            Text = text,
            Source = source,
            Reference = reference,
            Translation = translation,
            TranslationName = translationName,
            CreatedUtc = now,
            ModifiedUtc = now,
        };

        _passages.Add(passage);
        await PersistPassageAsync(passage, ct);
        Changed?.Invoke();

        return passage;
    }

    public async Task UpdatePassageAsync(Passage passage, CancellationToken ct = default)
    {
        var updated = passage with { ModifiedUtc = clock.UtcNow };
        var index = _passages.FindIndex(p => p.Id == passage.Id);

        if (index < 0)
        {
            _passages.Add(updated);
        }
        else
        {
            _passages[index] = updated;
        }

        await PersistPassageAsync(updated, ct);
        Changed?.Invoke();
    }

    public async Task DeletePassageAsync(Guid passageId, CancellationToken ct = default)
    {
        _passages.RemoveAll(p => p.Id == passageId);

        // Progress without its passage is unreachable, so it goes too.
        foreach (var key in _progress.Where(kv => kv.Value.PassageId == passageId).Select(kv => kv.Key).ToArray())
        {
            _progress.Remove(key);
        }

        if (providers.Active is IIncrementalStorageProvider incremental)
        {
            await incremental.DeletePassageAsync(passageId, ct);
        }
        else
        {
            await SaveSnapshotAsync(ct);
        }

        Changed?.Invoke();
    }

    public async Task SaveProgressAsync(PracticeProgress progress, CancellationToken ct = default)
    {
        // Stamped here rather than by callers, so every write is comparable on import.
        progress = progress with
        {
            UpdatedUtc = clock.UtcNow,
            Attempts = Trim(progress.Attempts),
        };

        _progress[progress.Key] = progress;

        if (providers.Active is IIncrementalStorageProvider incremental)
        {
            await incremental.UpsertProgressAsync(progress, ct);
        }
        else
        {
            await SaveSnapshotAsync(ct);
        }

        Changed?.Invoke();
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken ct = default)
    {
        Settings = settings;

        if (providers.Active is IIncrementalStorageProvider incremental)
        {
            await incremental.SaveSettingsAsync(settings, ct);
        }
        else
        {
            await SaveSnapshotAsync(ct);
        }

        Changed?.Invoke();
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        _passages.Clear();
        _progress.Clear();
        Settings = AppSettings.Default;

        await SaveSnapshotAsync(ct);
        Changed?.Invoke();
    }

    /// <summary>Builds the current library as a portable snapshot.</summary>
    public LibrarySnapshot ToSnapshot() => new()
    {
        SchemaVersion = LibrarySnapshot.CurrentSchemaVersion,
        ExportedUtc = clock.UtcNow,
        Passages = _passages.ToArray(),
        Progress = _progress.Values.ToArray(),
        Settings = Settings,
    };

    /// <summary>Hands the library to a provider that is not the active one, such as the save file.</summary>
    public Task ExportToAsync(IStorageProvider target, CancellationToken ct = default) =>
        target.SaveAsync(ToSnapshot(), ct);

    /// <summary>
    /// Brings in a snapshot read from elsewhere.
    ///
    /// On merge, an incoming passage only overwrites one already held if it is
    /// genuinely newer. Without that check, importing an old backup would silently
    /// roll back work the user has since done.
    /// </summary>
    public async Task<ImportResult> ImportAsync(
        LibrarySnapshot snapshot,
        ImportMode mode,
        CancellationToken ct = default)
    {
        if (mode == ImportMode.Replace)
        {
            Apply(snapshot);
            await SaveSnapshotAsync(ct);
            Changed?.Invoke();

            return new ImportResult(snapshot.Passages.Count, 0, 0, mode);
        }

        var added = 0;
        var updated = 0;
        var skipped = 0;

        foreach (var incoming in snapshot.Passages)
        {
            var index = _passages.FindIndex(p => p.Id == incoming.Id);

            if (index < 0)
            {
                _passages.Add(incoming);
                added++;
            }
            else if (incoming.ModifiedUtc > _passages[index].ModifiedUtc)
            {
                _passages[index] = incoming;
                updated++;
            }
            else
            {
                skipped++;
            }
        }

        // Guarded the same way passages are. Without this an older backup keeps the
        // newer passage but silently wipes the hidden words and attempt history that
        // belong to it — the exact rollback this method claims to prevent.
        foreach (var incoming in snapshot.Progress)
        {
            if (!_progress.TryGetValue(incoming.Key, out var existing)
                || incoming.UpdatedUtc > existing.UpdatedUtc)
            {
                _progress[incoming.Key] = incoming;
            }
        }

        await SaveSnapshotAsync(ct);
        Changed?.Invoke();

        return new ImportResult(added, updated, skipped, mode);
    }

    private async Task PersistPassageAsync(Passage passage, CancellationToken ct)
    {
        if (providers.Active is IIncrementalStorageProvider incremental)
        {
            await incremental.UpsertPassageAsync(passage, ct);
        }
        else
        {
            await SaveSnapshotAsync(ct);
        }
    }

    /// <summary>
    /// Caps stored attempts. They were appended forever while only the last few are
    /// ever shown, so a long-practised passage grew towards the localStorage quota
    /// for history nobody reads.
    /// </summary>
    private static IReadOnlyList<Attempt> Trim(IReadOnlyList<Attempt> attempts) =>
        attempts.Count <= MaxStoredAttempts
            ? attempts
            : [.. attempts.Skip(attempts.Count - MaxStoredAttempts)];

    private Task SaveSnapshotAsync(CancellationToken ct) => providers.Active.SaveAsync(ToSnapshot(), ct);

    private void Apply(LibrarySnapshot snapshot)
    {
        _passages.Clear();
        _passages.AddRange(snapshot.Passages);

        _progress.Clear();
        foreach (var progress in snapshot.Progress)
        {
            _progress[progress.Key] = progress;
        }

        Settings = snapshot.Settings;
    }
}
