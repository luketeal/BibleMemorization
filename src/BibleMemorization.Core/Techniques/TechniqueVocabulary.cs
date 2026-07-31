namespace BibleMemorization.Core.Techniques;

/// <summary>
/// The wording a technique uses for its study controls.
///
/// "Hide 10% more" is right for vanishing text and wrong for a technique whose
/// control drops initials. Putting the words on the technique keeps the practice
/// page from having to know which one it is rendering, which is the property the
/// whole abstraction exists to protect.
/// </summary>
/// <param name="HideMore">Takes a further slice of the passage away.</param>
/// <param name="HideOne">Takes one more word away.</param>
/// <param name="HideAll">Takes everything away.</param>
/// <param name="RevealAll">Puts everything back.</param>
/// <param name="StudyHint">One line telling the user what tapping a word does.</param>
public sealed record TechniqueVocabulary(
    string HideMore,
    string HideOne,
    string HideAll,
    string RevealAll,
    string StudyHint);
