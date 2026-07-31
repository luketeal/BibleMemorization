namespace BibleMemorization.App.Services;

/// <summary>
/// Whether the app is running for real or in demo mode.
///
/// Demo mode swaps the network and browser dependencies for fakes, which serves two
/// purposes at once: end-to-end tests get a deterministic, offline app to drive, and
/// the deployed site gets a link that works with canned passages regardless of API
/// availability or microphone permission.
/// </summary>
public sealed class AppMode(bool isDemo)
{
    public bool IsDemo { get; } = isDemo;

    public bool IsLive => !IsDemo;

    /// <summary>
    /// Builds an internal link that keeps the current mode. Demo mode lives in the
    /// query string, so a plain href would silently drop back into the real app on
    /// the first navigation.
    /// </summary>
    public string Link(string path)
    {
        if (!IsDemo)
        {
            return path;
        }

        return path.Contains('?') ? $"{path}&demo=1" : $"{path}?demo=1";
    }

    /// <summary>
    /// Reads the flag from the launch URL. Accepts "?demo=1" and "?demo=true", and
    /// tolerates it appearing after a fragment.
    /// </summary>
    public static AppMode FromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return new AppMode(false);
        }

        var query = url.Contains('?') ? url[(url.IndexOf('?') + 1)..] : string.Empty;

        if (query.Length == 0)
        {
            return new AppMode(false);
        }

        var fragmentStart = query.IndexOf('#');
        if (fragmentStart >= 0)
        {
            query = query[..fragmentStart];
        }

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);

            if (!string.Equals(parts[0], "demo", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // "?demo" alone counts as on.
            if (parts.Length == 1)
            {
                return new AppMode(true);
            }

            return new AppMode(parts[1] is "1" or "true" or "yes" or "on");
        }

        return new AppMode(false);
    }
}
