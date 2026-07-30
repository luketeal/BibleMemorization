namespace BibleMemorization.Core.Text;

/// <summary>
/// One unit of a passage. Tokens carry the whitespace and punctuation around them
/// so a passage can be reassembled exactly, which is what lets the UI hide a single
/// word without disturbing the surrounding text.
/// </summary>
/// <param name="Index">Position in the passage, used as the stable id for hiding.</param>
/// <param name="Leading">Whitespace preceding the token.</param>
/// <param name="Prefix">Opening punctuation, e.g. an opening quote or bracket.</param>
/// <param name="Word">The word itself, or the whole run for a non-word token.</param>
/// <param name="Suffix">Trailing punctuation, e.g. a comma or full stop.</param>
/// <param name="IsWord">
/// False for standalone punctuation and verse numbers. Non-word tokens are never
/// hidden and never scored.
/// </param>
public sealed record Token(
    int Index,
    string Leading,
    string Prefix,
    string Word,
    string Suffix,
    bool IsWord)
{
    /// <summary>The token exactly as it appeared in the source text.</summary>
    public string Original => Leading + Prefix + Word + Suffix;

    /// <summary>Visible characters only, without the preceding whitespace.</summary>
    public string Trimmed => Prefix + Word + Suffix;
}
