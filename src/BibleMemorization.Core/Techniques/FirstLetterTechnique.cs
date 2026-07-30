using BibleMemorization.Core.Text;

namespace BibleMemorization.Core.Techniques;

/// <summary>
/// Every word collapses to its first letter, so "For God so loved" reads "F G s l".
/// The initials carry the rhythm and shape of the passage while giving away almost
/// none of the words — a classic next step once vanishing text gets easy.
/// </summary>
public sealed class FirstLetterTechnique : IMemoryTechnique
{
    public const string TechniqueId = "first-letter";

    public string Id => TechniqueId;

    public string DisplayName => "First letter";

    public string Description =>
        "Every word shrinks to its first letter. The shape of the passage stays, the words go.";

    // This technique hides everything by definition, so tapping individual words
    // would have nothing to do.
    public bool SupportsManualWordSelection => false;

    public IReadOnlyList<DisplayToken> Render(TokenizedPassage passage, TechniqueState state)
    {
        var tokens = new List<DisplayToken>(passage.Count);

        foreach (var token in passage)
        {
            tokens.Add(token.IsWord
                ? DisplayToken.Abbreviated(token, token.Word[..1])
                : DisplayToken.Visible(token));
        }

        return tokens;
    }

    public IReadOnlyList<int> GetTestedTokenIndices(TokenizedPassage passage, TechniqueState state) =>
        passage.WordIndices;
}
