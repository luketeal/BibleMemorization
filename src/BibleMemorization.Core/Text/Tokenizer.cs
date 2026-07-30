using System.Globalization;

namespace BibleMemorization.Core.Text;

/// <summary>
/// Splits passage text into tokens, preserving every character so the original can
/// be rebuilt exactly.
/// </summary>
public static class Tokenizer
{
    public static TokenizedPassage Tokenize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new TokenizedPassage([]);
        }

        var tokens = new List<Token>();
        var position = 0;

        while (position < text.Length)
        {
            var leadingStart = position;
            while (position < text.Length && char.IsWhiteSpace(text[position]))
            {
                position++;
            }

            var leading = text[leadingStart..position];

            if (position >= text.Length)
            {
                // Whitespace runs to the end of the passage. Emit it as a token of its
                // own so reconstruction stays exact; it holds no word, so nothing will
                // ever try to hide or score it.
                tokens.Add(new Token(tokens.Count, leading, string.Empty, string.Empty, string.Empty, IsWord: false));
                break;
            }

            var chunkStart = position;
            while (position < text.Length && !char.IsWhiteSpace(text[position]))
            {
                position++;
            }

            tokens.Add(BuildToken(tokens.Count, leading, text[chunkStart..position]));
        }

        return new TokenizedPassage(tokens);
    }

    private static Token BuildToken(int index, string leading, string chunk)
    {
        // Peel punctuation off both ends. What remains in the middle is the word that
        // gets hidden and scored, while the punctuation stays visible so a blank still
        // reads as "____," rather than swallowing the comma. Peeling only at the edges
        // is also what keeps "God's" and "loving-kindness" intact as single words.
        var start = 0;
        while (start < chunk.Length && !IsWordCharacter(chunk[start]))
        {
            start++;
        }

        var end = chunk.Length;
        while (end > start && !IsWordCharacter(chunk[end - 1]))
        {
            end--;
        }

        var word = chunk[start..end];

        // A chunk with no word characters is standalone punctuation, such as an em
        // dash. A chunk that is all digits is a verse number. Neither is recallable.
        var isWord = word.Length > 0 && !word.All(char.IsDigit);

        return new Token(index, leading, chunk[..start], word, chunk[end..], isWord);
    }

    private static bool IsWordCharacter(char c) =>
        char.IsLetterOrDigit(c)
        || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark;
}
