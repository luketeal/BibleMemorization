using BibleMemorization.Core.Text;

namespace BibleMemorization.Core.Techniques;

/// <summary>
/// Every word collapses to its first letter, so "For God so loved" reads "F G s l".
/// The initials carry the rhythm and shape of the passage while giving away almost
/// none of the words.
///
/// It shares vanishing text's ladder, one rung further down. Vanishing text runs
/// full word to nothing; this runs initial to nothing, so a word the user has taken
/// away loses even its initial. An empty selection is the technique at its gentlest
/// — every word an initial — which is also exactly how it behaved before stages
/// existed, so saved progress carries over untouched.
/// </summary>
public sealed class FirstLetterTechnique : IMemoryTechnique
{
    public const string TechniqueId = "first-letter";

    public string Id => TechniqueId;

    public string DisplayName => "First letter";

    public string Description =>
        "Every word shrinks to its first letter. Drop the initials too as you get surer of it.";

    public bool SupportsManualWordSelection => true;

    // No word is ever shown in full, at any stage, so the app would have nothing to
    // read aloud.
    public bool SupportsReadAlong => false;

    public TechniqueVocabulary Vocabulary { get; } = new(
        HideMore: "Drop 10% more initials",
        HideOne: "Drop one initial",
        HideAll: "Drop all initials",
        RevealAll: "Show all initials",
        StudyHint: "Tap an initial to drop it entirely. Tap the blank to bring it back.");

    public IReadOnlyList<DisplayToken> Render(TokenizedPassage passage, TechniqueState state)
    {
        var tokens = new List<DisplayToken>(passage.Count);

        foreach (var token in passage)
        {
            if (!token.IsWord)
            {
                tokens.Add(DisplayToken.Visible(token));
                continue;
            }

            tokens.Add(state.IsHidden(token.Index)
                ? DisplayToken.Blank(token)
                : DisplayToken.Abbreviated(token, token.Word[..1]));
        }

        return tokens;
    }

    // Every word is being recalled, whether or not its initial is still showing.
    public IReadOnlyList<int> GetTestedTokenIndices(TokenizedPassage passage, TechniqueState state) =>
        passage.WordIndices;
}
