using BibleMemorization.Core.Storage;

namespace BibleMemorization.Core.Tests.Storage;

public class StorageProviderRegistryTests
{
    /// <summary>Stands in for the save file: explicit gesture only, never a background sync.</summary>
    private sealed class GestureOnlyProvider : IStorageProvider
    {
        public string Id => "savefile";

        public string DisplayName => "Save file";

        public StorageCapabilities Capabilities =>
            StorageCapabilities.ReadWrite | StorageCapabilities.RequiresUserGesture;

        public Task<LibrarySnapshot> LoadAsync(CancellationToken ct = default) =>
            Task.FromResult(LibrarySnapshot.Empty);

        public Task SaveAsync(LibrarySnapshot snapshot, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private static StorageProviderRegistry Registry() =>
        new([new InMemoryStorageProvider(), new GestureOnlyProvider()]);

    [Fact]
    public void Providers_resolve_by_id()
    {
        var registry = Registry();

        Assert.IsType<InMemoryStorageProvider>(registry.Get(InMemoryStorageProvider.ProviderId));
        Assert.IsType<GestureOnlyProvider>(registry.Get("savefile"));
    }

    [Fact]
    public void An_unknown_id_falls_back_to_the_active_provider()
    {
        var registry = Registry();

        Assert.Same(registry.Active, registry.Get("no-such-provider"));
    }

    [Fact]
    public void The_first_registered_provider_starts_active()
    {
        Assert.IsType<InMemoryStorageProvider>(Registry().Active);
    }

    [Fact]
    public void Only_auto_saving_providers_are_offered_as_the_live_backend()
    {
        var registry = Registry();

        Assert.Equal(["memory"], registry.AutoSaving.Select(p => p.Id));
    }

    [Fact]
    public void The_active_provider_can_be_switched()
    {
        var registry = Registry();

        registry.SetActive(InMemoryStorageProvider.ProviderId);

        Assert.Equal(InMemoryStorageProvider.ProviderId, registry.Active.Id);
    }

    /// <summary>
    /// Making the save file the live backend would mean a file download on every
    /// keystroke, so the registry refuses rather than letting the UI try.
    /// </summary>
    [Fact]
    public void A_gesture_only_provider_cannot_become_the_live_backend()
    {
        var registry = Registry();

        var ex = Assert.Throws<ArgumentException>(() => registry.SetActive("savefile"));

        Assert.Contains("gesture", ex.Message);
    }

    [Fact]
    public void Switching_to_an_unregistered_provider_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Registry().SetActive("database"));
    }

    [Fact]
    public void An_empty_registry_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new StorageProviderRegistry([]));
    }
}
