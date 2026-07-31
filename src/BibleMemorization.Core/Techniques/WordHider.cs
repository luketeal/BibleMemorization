using BibleMemorization.Core.Text;

namespace BibleMemorization.Core.Techniques;

/// <summary>
/// The bulk hide and reveal operations behind the practice controls.
///
/// Selection is seeded rather than ambiently random so a given passage and seed
/// always hide the same words. That keeps tests deterministic and screenshots
/// diffable, and lets a practice session be reproduced exactly.
/// </summary>
public static class WordHider
{
    /// <summary>Hides every word in the passage.</summary>
    public static IReadOnlyList<int> HideAll(TokenizedPassage passage) => passage.WordIndices;

    /// <summary>Reveals everything.</summary>
    public static IReadOnlyList<int> RevealAll() => [];

    /// <summary>
    /// Hides <paramref name="count"/> more words chosen from those still visible.
    /// Asking for more than remain simply hides the rest.
    /// </summary>
    public static IReadOnlyList<int> HideMore(
        TokenizedPassage passage,
        IEnumerable<int> currentlyHidden,
        int count,
        int seed)
    {
        var hidden = new HashSet<int>(currentlyHidden);
        var candidates = passage.WordIndices.Where(i => !hidden.Contains(i)).ToList();

        if (count <= 0 || candidates.Count == 0)
        {
            return [.. hidden.OrderBy(i => i)];
        }

        var random = new Random(seed);

        // Fisher-Yates over the candidates, taking as many as were asked for.
        for (var i = candidates.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        foreach (var index in candidates.Take(Math.Min(count, candidates.Count)))
        {
            hidden.Add(index);
        }

        return [.. hidden.OrderBy(i => i)];
    }

    /// <summary>
    /// Hides a further percentage of the passage's words, rounded up so that a small
    /// passage or a small percentage still hides at least one word — otherwise the
    /// button would appear to do nothing.
    /// </summary>
    public static IReadOnlyList<int> HideMorePercent(
        TokenizedPassage passage,
        IEnumerable<int> currentlyHidden,
        double percent,
        int seed)
    {
        var hidden = currentlyHidden.ToArray();
        var count = (int)Math.Ceiling(passage.WordCount * percent);
        return HideMore(passage, hidden, Math.Max(count, 1), seed);
    }

    /// <summary>Toggles one word between hidden and visible.</summary>
    public static IReadOnlyList<int> Toggle(
        TokenizedPassage passage,
        IEnumerable<int> currentlyHidden,
        int tokenIndex)
    {
        var hidden = new HashSet<int>(currentlyHidden);

        if (tokenIndex < 0 || tokenIndex >= passage.Count || !passage[tokenIndex].IsWord)
        {
            return [.. hidden.OrderBy(i => i)];
        }

        if (!hidden.Remove(tokenIndex))
        {
            hidden.Add(tokenIndex);
        }

        return [.. hidden.OrderBy(i => i)];
    }
}
