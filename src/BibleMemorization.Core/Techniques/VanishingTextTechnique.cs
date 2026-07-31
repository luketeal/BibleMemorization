using BibleMemorization.Core.Text;

namespace BibleMemorization.Core.Techniques;

/// <summary>
/// The user taps words to make them vanish, leaving a blank the width of the word.
/// Hiding more words over successive rounds is what drives the passage into memory.
/// </summary>
public sealed class VanishingTextTechnique : IMemoryTechnique
{
    public const string TechniqueId = "vanishing-text";

    public string Id => TechniqueId;

    public string DisplayName => "Vanishing text";

    public string Description =>
        "Tap words to hide them. Each one leaves a blank you have to fill from memory.";

    public bool SupportsManualWordSelection => true;

    public bool SupportsReadAlong => true;

    public TechniqueVocabulary Vocabulary { get; } = new(
        HideMore: "Hide 10% more",
        HideOne: "Hide one",
        HideAll: "Hide all",
        RevealAll: "Reveal all",
        StudyHint: "Tap any word to make it vanish. Tap a blank to bring it back.");

    public IReadOnlyList<DisplayToken> Render(TokenizedPassage passage, TechniqueState state)
    {
        var tokens = new List<DisplayToken>(passage.Count);

        foreach (var token in passage)
        {
            // Non-word tokens stay put no matter what: hiding a comma teaches nothing
            // and would make the remaining text hard to read.
            tokens.Add(token.IsWord && state.IsHidden(token.Index)
                ? DisplayToken.Blank(token)
                : DisplayToken.Visible(token));
        }

        return tokens;
    }

    public IReadOnlyList<int> GetTestedTokenIndices(TokenizedPassage passage, TechniqueState state) =>
        passage.WordIndices.Where(state.IsHidden).ToArray();
}
