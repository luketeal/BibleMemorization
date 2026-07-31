using System.Text;
using System.Text.Json;

namespace BibleMemorization.Core.Bible;

/// <summary>
/// Turns the API's structured chapter into plain verses.
///
/// A chapter's content array mixes verses, section headings and line breaks, and a
/// verse's own content mixes bare strings, formatted-text objects and footnote
/// references. Only the words belong in something you memorize: headings are
/// editorial, and a stray footnote marker would become a "word" the user is asked
/// to recall.
/// </summary>
public static class ChapterFlattener
{
    public static IReadOnlyList<Verse> Flatten(ChapterBody? chapter)
    {
        if (chapter is null)
        {
            return [];
        }

        var verses = new List<Verse>();

        foreach (var entry in chapter.Content)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!entry.TryGetProperty("type", out var type) || type.GetString() != "verse")
            {
                // Headings and line breaks are structure, not scripture.
                continue;
            }

            var number = entry.TryGetProperty("number", out var n) && n.TryGetInt32(out var parsed) ? parsed : 0;
            var text = FlattenVerseContent(entry);

            if (!string.IsNullOrWhiteSpace(text))
            {
                verses.Add(new Verse(number, text));
            }
        }

        return verses;
    }

    /// <summary>
    /// Joins a verse and returns it as one block of text, optionally limited to a
    /// verse range.
    /// </summary>
    public static string ToPlainText(
        IReadOnlyList<Verse> verses,
        int? firstVerse = null,
        int? lastVerse = null)
    {
        var selected = verses
            .Where(v => (firstVerse is null || v.Number >= firstVerse)
                     && (lastVerse is null || v.Number <= lastVerse))
            .Select(v => v.Text);

        return string.Join(" ", selected).Trim();
    }

    private static string FlattenVerseContent(JsonElement verse)
    {
        if (!verse.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        foreach (var part in content.EnumerateArray())
        {
            var text = part.ValueKind switch
            {
                JsonValueKind.String => part.GetString(),

                // A formatted run carries its words under "text"; a footnote
                // reference carries only "noteId" and contributes nothing.
                JsonValueKind.Object => part.TryGetProperty("text", out var t) ? t.GetString() : null,

                _ => null,
            };

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(text.Trim());
        }

        return builder.ToString();
    }
}
