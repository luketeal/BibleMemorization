using System.Globalization;
using System.Text.RegularExpressions;

namespace BibleMemorization.Core.Bible;

/// <summary>
/// Turns what someone types — "John 3:16-18", "1 Cor 13", "psalm 23:1" — into a
/// reference the API can be asked for.
///
/// Matching is done against the book list the API itself returned rather than a
/// hard-coded canon, so it stays correct across translations that name or order
/// books differently. The abbreviation table only supplements that.
/// </summary>
public static partial class ReferenceParser
{
    /// <summary>
    /// Book name, then chapter, then an optional verse or verse range.
    /// The book part allows a leading numeral so "1 Corinthians" and "2 John" work.
    /// </summary>
    [GeneratedRegex(
        @"^\s*(?<book>(?:[1-3]|I{1,3})?\s*[\p{L}][\p{L}\s\.]*?)\s*(?<chapter>\d+)\s*(?::\s*(?<first>\d+)\s*(?:\s*[-–—]\s*(?<last>\d+))?)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReferencePattern { get; }

    /// <summary>A leading 1-3 immediately followed by letters, as in "1cor".</summary>
    [GeneratedRegex(@"^([1-3])\s*(?=\p{L})", RegexOptions.CultureInvariant)]
    private static partial Regex NumeralPrefixPattern { get; }

    /// <summary>
    /// Common short forms that are not just a prefix of the full name, so prefix
    /// matching alone would miss them.
    /// </summary>
    private static readonly Dictionary<string, string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gen"] = "genesis",
        ["ex"] = "exodus",
        ["exod"] = "exodus",
        ["lev"] = "leviticus",
        ["num"] = "numbers",
        ["deut"] = "deuteronomy",
        ["dt"] = "deuteronomy",
        ["josh"] = "joshua",
        ["judg"] = "judges",
        ["sam"] = "samuel",
        ["kgs"] = "kings",
        ["chr"] = "chronicles",
        ["chron"] = "chronicles",
        ["neh"] = "nehemiah",
        ["ps"] = "psalms",
        ["psa"] = "psalms",
        ["psalm"] = "psalms",
        ["pss"] = "psalms",
        ["prov"] = "proverbs",
        ["prv"] = "proverbs",
        ["eccl"] = "ecclesiastes",
        ["song"] = "song of solomon",
        ["isa"] = "isaiah",
        ["jer"] = "jeremiah",
        ["lam"] = "lamentations",
        ["ezek"] = "ezekiel",
        ["dan"] = "daniel",
        ["hos"] = "hosea",
        ["obad"] = "obadiah",
        ["mic"] = "micah",
        ["nah"] = "nahum",
        ["hab"] = "habakkuk",
        ["zeph"] = "zephaniah",
        ["hag"] = "haggai",
        ["zech"] = "zechariah",
        ["mal"] = "malachi",
        ["matt"] = "matthew",
        ["mt"] = "matthew",
        ["mk"] = "mark",
        ["lk"] = "luke",
        ["jn"] = "john",
        ["jhn"] = "john",
        ["rom"] = "romans",
        ["cor"] = "corinthians",
        ["gal"] = "galatians",
        ["eph"] = "ephesians",
        ["phil"] = "philippians",
        ["php"] = "philippians",
        ["col"] = "colossians",
        ["thess"] = "thessalonians",
        ["thes"] = "thessalonians",
        ["tim"] = "timothy",
        ["tit"] = "titus",
        ["philem"] = "philemon",
        ["heb"] = "hebrews",
        ["jas"] = "james",
        ["pet"] = "peter",
        ["ptr"] = "peter",
        ["rev"] = "revelation",
        ["apoc"] = "revelation",
    };

    /// <summary>
    /// Parses a reference against a known book list. Returns null when the input is
    /// not a reference at all or names no book in the list — the caller falls back
    /// to the dropdown pickers rather than guessing.
    /// </summary>
    public static BibleReference? Parse(string? input, IReadOnlyList<BibleBook> books)
    {
        if (string.IsNullOrWhiteSpace(input) || books.Count == 0)
        {
            return null;
        }

        var match = ReferencePattern.Match(input);

        if (!match.Success)
        {
            return null;
        }

        var book = FindBook(match.Groups["book"].Value, books);

        if (book is null)
        {
            return null;
        }

        var chapter = int.Parse(match.Groups["chapter"].Value, CultureInfo.InvariantCulture);

        if (chapter < 1 || (book.NumberOfChapters > 0 && chapter > book.NumberOfChapters))
        {
            return null;
        }

        int? first = match.Groups["first"].Success
            ? int.Parse(match.Groups["first"].Value, CultureInfo.InvariantCulture)
            : null;

        int? last = match.Groups["last"].Success
            ? int.Parse(match.Groups["last"].Value, CultureInfo.InvariantCulture)
            : null;

        if (first is < 1 || last is < 1)
        {
            return null;
        }

        // "John 3:18-16" is a slip rather than a request for nothing, so read it in
        // the order the user plainly meant.
        if (first is not null && last is not null && last < first)
        {
            (first, last) = (last, first);
        }

        return new BibleReference(book.Id, book.DisplayName, chapter, first, last);
    }

    private static BibleBook? FindBook(string raw, IReadOnlyList<BibleBook> books)
    {
        var normalized = NormalizeBookName(raw);

        if (normalized.Length == 0)
        {
            return null;
        }

        // Exact name first, so "John" never resolves to "1 John".
        var exact = books.FirstOrDefault(b =>
            NormalizeBookName(b.DisplayName) == normalized || NormalizeBookName(b.Name) == normalized);

        if (exact is not null)
        {
            return exact;
        }

        var expanded = ExpandAbbreviation(normalized);

        if (expanded != normalized)
        {
            var viaAbbreviation = books.FirstOrDefault(b =>
                NormalizeBookName(b.DisplayName) == expanded || NormalizeBookName(b.Name) == expanded);

            if (viaAbbreviation is not null)
            {
                return viaAbbreviation;
            }
        }

        // Fall back to a prefix match, which catches truncations the table misses.
        var prefixMatches = books
            .Where(b => NormalizeBookName(b.DisplayName).StartsWith(expanded, StringComparison.Ordinal))
            .ToArray();

        // Ambiguous prefixes such as "jo" could be John, Jonah or Joel. Refusing is
        // better than silently importing the wrong book.
        return prefixMatches.Length == 1 ? prefixMatches[0] : null;
    }

    /// <summary>
    /// Lowercases, drops full stops, collapses whitespace, and turns Roman numeral
    /// prefixes into digits so "I Corinthians" and "1 Corinthians" agree.
    /// </summary>
    private static string NormalizeBookName(string raw)
    {
        var cleaned = raw.Replace(".", string.Empty, StringComparison.Ordinal).Trim().ToLowerInvariant();

        // People type "1cor" as readily as "1 Cor", so separate a leading numeral
        // from the name before anything else looks at the words.
        cleaned = NumeralPrefixPattern.Replace(cleaned, "$1 ");

        var parts = cleaned.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return string.Empty;
        }

        parts[0] = parts[0] switch
        {
            "i" => "1",
            "ii" => "2",
            "iii" => "3",
            var other => other,
        };

        return string.Join(' ', parts);
    }

    /// <summary>
    /// Expands the last word through the abbreviation table, keeping any numeral
    /// prefix, so "1 cor" becomes "1 corinthians".
    /// </summary>
    private static string ExpandAbbreviation(string normalized)
    {
        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return normalized;
        }

        if (!Abbreviations.TryGetValue(parts[^1], out var expanded))
        {
            return normalized;
        }

        parts[^1] = expanded;
        return string.Join(' ', parts);
    }
}
