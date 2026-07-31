using BibleMemorization.App.Services;

namespace BibleMemorization.Core.Tests;

public class AppModeTests
{
    [Theory]
    [InlineData("https://example.test/?demo=1")]
    [InlineData("https://example.test/?demo=true")]
    [InlineData("https://example.test/?demo=yes")]
    [InlineData("https://example.test/?demo=on")]
    [InlineData("https://example.test/?demo")]
    [InlineData("https://example.test/BibleMemorization/?demo=1")]
    [InlineData("https://example.test/?other=x&demo=1")]
    [InlineData("https://example.test/?demo=1#practice")]
    [InlineData("https://example.test/?DEMO=1")]
    public void The_demo_flag_is_recognised(string url)
    {
        Assert.True(AppMode.FromUrl(url).IsDemo);
    }

    [Theory]
    [InlineData("https://example.test/")]
    [InlineData("https://example.test/?demo=0")]
    [InlineData("https://example.test/?demo=false")]
    [InlineData("https://example.test/?other=1")]
    [InlineData("https://example.test/#demo=1")]
    [InlineData("")]
    [InlineData(null)]
    public void Everything_else_runs_for_real(string? url)
    {
        var mode = AppMode.FromUrl(url!);

        Assert.False(mode.IsDemo);
        Assert.True(mode.IsLive);
    }
}
