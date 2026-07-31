using System.Globalization;
using BibleMemorization.Core.Text;

namespace BibleMemorization.Core.Tests.Text;

public class PercentTests
{
    [Theory]
    [InlineData(1.0, "100%")]
    [InlineData(0.0, "0%")]
    [InlineData(0.5, "50%")]
    [InlineData(0.96, "96%")]
    [InlineData(0.955, "96%")]
    [InlineData(0.333333, "33%")]
    public void Percentages_format_without_a_space(double fraction, string expected)
    {
        Assert.Equal(expected, Percent.Format(fraction));
    }

    /// <summary>
    /// The bug this replaced. The "P0" format follows the ambient culture, and
    /// cultures disagree about the percent sign - invariant culture writes "100 %"
    /// with a space. In Blazor WebAssembly the culture comes from the browser, so
    /// the same build rendered differently on different machines. Six end-to-end
    /// tests passed locally and failed in CI for exactly this reason.
    /// </summary>
    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    [InlineData("sv-SE")]
    [InlineData("")]
    public void The_format_does_not_change_with_the_current_culture(string cultureName)
    {
        var original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = cultureName.Length == 0
                ? CultureInfo.InvariantCulture
                : new CultureInfo(cultureName);

            Assert.Equal("100%", Percent.Format(1.0));
            Assert.Equal("96%", Percent.Format(0.96));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void A_half_rounds_up_rather_than_to_even()
    {
        // Banker's rounding would make this 0, which reads as a total failure.
        Assert.Equal(1, Percent.ToWhole(0.005));
        Assert.Equal(51, Percent.ToWhole(0.505));
    }

    [Theory]
    [InlineData(-0.5, 0)]
    [InlineData(1.5, 100)]
    [InlineData(double.NaN, 0)]
    public void Values_outside_the_range_are_handled_rather_than_shown_raw(double fraction, int expected)
    {
        Assert.Equal(expected, Percent.ToWhole(fraction));
    }
}
