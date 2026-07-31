using System.Globalization;

namespace BibleMemorization.Core.Text;

/// <summary>
/// Formats accuracy for display.
///
/// Deliberately not <c>"P0"</c>. That format follows the ambient culture, and
/// cultures disagree about the percent sign: some render "100%", others "100 %"
/// with a space. In Blazor WebAssembly the culture comes from the browser, so the
/// same build renders differently on different machines — which is confusing when
/// every other label in the app is English, and makes tests depend on the locale of
/// whatever machine happens to run them.
/// </summary>
public static class Percent
{
    /// <summary>Whole-number percentage, e.g. 0.955 becomes "96%".</summary>
    public static string Format(double fraction) =>
        string.Create(CultureInfo.InvariantCulture, $"{ToWhole(fraction)}%");

    /// <summary>The percentage as a whole number, rounded.</summary>
    public static int ToWhole(double fraction)
    {
        if (double.IsNaN(fraction))
        {
            return 0;
        }

        var clamped = Math.Clamp(fraction, 0d, 1d);

        // Away-from-zero so a half rounds up, matching what a reader expects rather
        // than banker's rounding turning 0.5 into 0.
        return (int)Math.Round(clamped * 100, MidpointRounding.AwayFromZero);
    }
}
