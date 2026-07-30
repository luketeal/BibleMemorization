namespace BibleMemorization.E2E.Infrastructure;

/// <summary>
/// Locates repository paths from the test assembly's location, so tests do not
/// depend on the working directory the runner happens to use.
/// </summary>
public static class RepoLayout
{
    public static string Root { get; } = FindRoot();

    public static string AppProject =>
        Path.Combine(Root, "src", "BibleMemorization.App", "BibleMemorization.App.csproj");

    public static string ArtifactsDir => Path.Combine(Root, "artifacts");

    public static string ScreenshotsDir => Path.Combine(ArtifactsDir, "screenshots");

    public static string FailuresDir => Path.Combine(ArtifactsDir, "e2e");

    public static string EnsureDir(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BibleMemorization.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root above '{AppContext.BaseDirectory}'.");
    }
}
