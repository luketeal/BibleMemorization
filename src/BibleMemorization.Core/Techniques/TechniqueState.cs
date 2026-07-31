namespace BibleMemorization.Core.Techniques;

/// <summary>
/// The per-passage, per-technique practice state a technique reads when rendering.
/// </summary>
public sealed class TechniqueState
{
    public TechniqueState(IEnumerable<int>? hiddenTokenIndices = null)
    {
        HiddenTokenIndices = hiddenTokenIndices is null ? [] : [.. hiddenTokenIndices];
    }

    /// <summary>Token indices the user has chosen to hide.</summary>
    public HashSet<int> HiddenTokenIndices { get; }

    public bool IsHidden(int tokenIndex) => HiddenTokenIndices.Contains(tokenIndex);

    public TechniqueState With(IEnumerable<int> hidden) => new(hidden);
}
