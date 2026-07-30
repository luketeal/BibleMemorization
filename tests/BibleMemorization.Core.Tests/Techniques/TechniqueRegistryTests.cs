using BibleMemorization.Core.Techniques;

namespace BibleMemorization.Core.Tests.Techniques;

public class TechniqueRegistryTests
{
    private static TechniqueRegistry Registry() =>
        new([new VanishingTextTechnique(), new FirstLetterTechnique()]);

    [Fact]
    public void Techniques_resolve_by_id()
    {
        var registry = Registry();

        Assert.IsType<VanishingTextTechnique>(registry.Get(VanishingTextTechnique.TechniqueId));
        Assert.IsType<FirstLetterTechnique>(registry.Get(FirstLetterTechnique.TechniqueId));
    }

    [Fact]
    public void An_unknown_or_missing_id_falls_back_to_the_default()
    {
        var registry = Registry();

        // Progress saved against a technique that no longer exists must still open
        // rather than crashing the practice page.
        Assert.Same(registry.Default, registry.Get("technique-that-was-removed"));
        Assert.Same(registry.Default, registry.Get(null));
    }

    [Fact]
    public void Vanishing_text_is_the_default()
    {
        Assert.IsType<VanishingTextTechnique>(Registry().Default);
    }

    [Fact]
    public void All_registered_techniques_are_listed()
    {
        Assert.Equal(2, Registry().All.Count);
    }

    [Fact]
    public void Registered_ids_are_reported()
    {
        var registry = Registry();

        Assert.True(registry.Contains(VanishingTextTechnique.TechniqueId));
        Assert.False(registry.Contains("nope"));
    }

    [Fact]
    public void An_empty_registry_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new TechniqueRegistry([]));
    }

    [Fact]
    public void Technique_ids_are_unique()
    {
        var registry = Registry();

        Assert.Equal(registry.All.Count, registry.All.Select(t => t.Id).Distinct().Count());
    }
}
