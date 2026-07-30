namespace BibleMemorization.Core.Storage;

/// <summary>
/// Resolves storage providers by id, and holds which one is currently live.
/// Adding a database backend means registering one more implementation here.
/// </summary>
public sealed class StorageProviderRegistry
{
    private readonly Dictionary<string, IStorageProvider> _byId;

    public StorageProviderRegistry(IEnumerable<IStorageProvider> providers)
    {
        All = providers.ToArray();

        if (All.Count == 0)
        {
            throw new ArgumentException("At least one storage provider must be registered.", nameof(providers));
        }

        _byId = All.ToDictionary(p => p.Id, StringComparer.Ordinal);
        Active = All[0];
    }

    public IReadOnlyList<IStorageProvider> All { get; }

    /// <summary>The provider the library currently reads from and writes to.</summary>
    public IStorageProvider Active { get; private set; }

    /// <summary>
    /// Providers that can hold the library in the background. The save file is
    /// excluded, since it only acts on an explicit click.
    /// </summary>
    public IEnumerable<IStorageProvider> AutoSaving =>
        All.Where(p => p.Capabilities.HasFlag(StorageCapabilities.AutoSave));

    public IStorageProvider Get(string? id) =>
        id is not null && _byId.TryGetValue(id, out var provider) ? provider : Active;

    public bool Contains(string id) => _byId.ContainsKey(id);

    public void SetActive(string id)
    {
        if (!_byId.TryGetValue(id, out var provider))
        {
            throw new ArgumentException($"No storage provider registered with id '{id}'.", nameof(id));
        }

        if (!provider.Capabilities.HasFlag(StorageCapabilities.AutoSave))
        {
            throw new ArgumentException(
                $"'{id}' only acts on an explicit user gesture and cannot back the live library.",
                nameof(id));
        }

        Active = provider;
    }
}
