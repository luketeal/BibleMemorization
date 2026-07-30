using System.Globalization;
using System.Text;

namespace BibleMemorization.Core.Text;

/// <summary>
/// Reduces words to a comparable form. Every comparison in the app runs through
/// here so that typed input and speech transcripts are judged by the same rules —
/// speech recognizers never return punctuation or capitalisation, and a user typing
/// "thou" should not be marked wrong against "Thou,".
/// </summary>
public static class TextNormalizer
{
    /// <summary>Normalizes a single word for equality comparison.</summary>
    public static string NormalizeWord(string? word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return string.Empty;
        }

        // Decompose so accents become separate marks that can be dropped, letting
        // a plain-ASCII typist match accented source text.
        var decomposed = word.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }

            // Everything else - punctuation, curly quotes, hyphens - is dropped, so
            // "loving-kindness" and "lovingkindness" compare equal.
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Splits free text into normalized words, discarding anything empty.</summary>
    public static IReadOnlyList<string> NormalizeWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeWord)
            .Where(w => w.Length > 0)
            .ToArray();
    }

    /// <summary>True when two words match once normalized.</summary>
    public static bool WordsMatch(string? left, string? right)
    {
        var normalizedLeft = NormalizeWord(left);
        return normalizedLeft.Length > 0 && normalizedLeft == NormalizeWord(right);
    }
}
