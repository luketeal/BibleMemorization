using System.Collections;

namespace BibleMemorization.Core.Text;

/// <summary>
/// A passage split into tokens. Guarantees exact reconstruction of the source text.
/// </summary>
public sealed class TokenizedPassage : IReadOnlyList<Token>
{
    private readonly IReadOnlyList<Token> _tokens;

    public TokenizedPassage(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens;
        WordIndices = tokens.Where(t => t.IsWord).Select(t => t.Index).ToArray();
    }

    public Token this[int index] => _tokens[index];

    public int Count => _tokens.Count;

    /// <summary>Indices of tokens that are real words, in order.</summary>
    public IReadOnlyList<int> WordIndices { get; }

    /// <summary>Number of hideable words in the passage.</summary>
    public int WordCount => WordIndices.Count;

    /// <summary>Rebuilds the original text exactly.</summary>
    public string ToOriginalText() => string.Concat(_tokens.Select(t => t.Original));

    public IEnumerator<Token> GetEnumerator() => _tokens.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
