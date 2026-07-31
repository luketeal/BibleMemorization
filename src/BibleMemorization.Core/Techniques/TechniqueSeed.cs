namespace BibleMemorization.Core.Techniques;

/// <summary>
/// The seed handed to <see cref="WordHider"/> when the user asks for more words to
/// be hidden.
///
/// It has to do two things at once: vary as words accumulate, so pressing "hide 20%"
/// twice hides different words, and stay identical across page loads, so reopening a
/// passage does not reshuffle it.
///
/// It lives here rather than in the page because <c>HashCode.Combine</c> quietly
/// broke the second half. .NET randomizes string hashing per process, so folding in
/// the technique id gave a different seed on every load — reproducible in a unit
/// test that ran it twice in one process, and wrong in the browser.
/// </summary>
public static class TechniqueSeed
{
    public static int For(Guid passageId, string techniqueId, int hiddenCount)
    {
        // Folded byte by byte rather than through Guid.GetHashCode, which xors halves
        // of the value together and so collapses to the same number for guids with a
        // repeating pattern. Stable across processes either way; this one just does
        // not throw information away.
        Span<byte> bytes = stackalloc byte[16];
        passageId.TryWriteBytes(bytes);

        var seed = 17;

        foreach (var b in bytes)
        {
            seed = unchecked((seed * 31) + b);
        }

        seed ^= hiddenCount * 397;

        foreach (var c in techniqueId)
        {
            seed = unchecked((seed * 31) + c);
        }

        return seed;
    }
}
