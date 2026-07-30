namespace BibleMemorization.Core.Bible;

/// <summary>
/// A parsed scripture reference.
/// </summary>
/// <param name="BookId">Book code from the API, e.g. "JHN".</param>
/// <param name="BookName">Display name, for building the passage title.</param>
/// <param name="Chapter">Chapter number.</param>
/// <param name="FirstVerse">Null means the whole chapter.</param>
/// <param name="LastVerse">Null with a first verse means that single verse.</param>
public sealed record BibleReference(
    string BookId,
    string BookName,
    int Chapter,
    int? FirstVerse = null,
    int? LastVerse = null)
{
    public bool IsWholeChapter => FirstVerse is null;

    /// <summary>Canonical display form, e.g. "John 3:16-18".</summary>
    public override string ToString()
    {
        if (FirstVerse is null)
        {
            return $"{BookName} {Chapter}";
        }

        if (LastVerse is null || LastVerse == FirstVerse)
        {
            return $"{BookName} {Chapter}:{FirstVerse}";
        }

        return $"{BookName} {Chapter}:{FirstVerse}-{LastVerse}";
    }
}
