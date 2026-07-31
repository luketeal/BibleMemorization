using BibleMemorization.Core.Text;

namespace BibleMemorization.Core.Techniques;

/// <summary>
/// A way of progressively removing text so it has to be recalled.
///
/// Adding a technique means writing one class and registering it — no UI changes,
/// because the practice view only ever consumes <see cref="DisplayToken"/>s.
/// </summary>
public interface IMemoryTechnique
{
    /// <summary>Stable id, persisted with progress. Never change it for a shipped technique.</summary>
    string Id { get; }

    string DisplayName { get; }

    string Description { get; }

    /// <summary>
    /// True when the user picks which words vanish. False for techniques that decide
    /// on their own, so the UI knows whether clicking a word should do anything.
    /// </summary>
    bool SupportsManualWordSelection { get; }

    /// <summary>
    /// True when guided read-along makes sense.
    ///
    /// Read-along is defined as "the app reads the words still showing and pauses at
    /// the rest". A technique that never shows a word in full gives it nothing to
    /// read, so offering it would be offering something that cannot work.
    /// </summary>
    bool SupportsReadAlong { get; }

    /// <summary>Wording for the study controls, since techniques take words away differently.</summary>
    TechniqueVocabulary Vocabulary { get; }

    /// <summary>Produces the view of the passage the user should study or be tested on.</summary>
    IReadOnlyList<DisplayToken> Render(TokenizedPassage passage, TechniqueState state);

    /// <summary>
    /// The word indices this technique is currently testing, in passage order. The
    /// scorer and the read-along planner both work from this, so each technique
    /// decides what "being tested" means for it.
    /// </summary>
    IReadOnlyList<int> GetTestedTokenIndices(TokenizedPassage passage, TechniqueState state);
}
