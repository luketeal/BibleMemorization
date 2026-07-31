using BibleMemorization.Core.Text;

namespace BibleMemorization.Core.Techniques;

/// <summary>
/// One token as the practice view should draw it.
///
/// The UI renders a list of these without knowing which technique produced them,
/// which is what keeps techniques pluggable: a new technique changes what the user
/// sees without touching a single component.
/// </summary>
/// <param name="TokenIndex">Index into the source <see cref="TokenizedPassage"/>.</param>
/// <param name="Leading">Whitespace to emit before this token.</param>
/// <param name="Prefix">Punctuation shown before the word, always visible.</param>
/// <param name="Text">
/// What to draw in place of the word: the word itself when visible, an abbreviated
/// form for techniques like first-letter, or empty when it is a blank.
/// </param>
/// <param name="Suffix">Punctuation shown after the word, always visible.</param>
/// <param name="IsBlank">True when the user must supply this word.</param>
/// <param name="IsWord">False for punctuation and verse numbers, which are never testable.</param>
/// <param name="BlankWidth">
/// Character count of the hidden word, so a blank can be drawn at roughly the width
/// of what it conceals — a length cue is part of what makes vanishing text learnable.
/// </param>
public sealed record DisplayToken(
    int TokenIndex,
    string Leading,
    string Prefix,
    string Text,
    string Suffix,
    bool IsBlank,
    bool IsWord,
    int BlankWidth)
{
    public static DisplayToken Visible(Token token) => new(
        token.Index,
        token.Leading,
        token.Prefix,
        token.Word,
        token.Suffix,
        IsBlank: false,
        token.IsWord,
        BlankWidth: 0);

    public static DisplayToken Blank(Token token) => new(
        token.Index,
        token.Leading,
        token.Prefix,
        string.Empty,
        token.Suffix,
        IsBlank: true,
        token.IsWord,
        token.Word.Length);

    public static DisplayToken Abbreviated(Token token, string text) => new(
        token.Index,
        token.Leading,
        token.Prefix,
        text,
        token.Suffix,
        IsBlank: true,
        token.IsWord,
        token.Word.Length);
}
