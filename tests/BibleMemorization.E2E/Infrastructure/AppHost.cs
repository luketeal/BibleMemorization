using System.Diagnostics;
using System.Net.Sockets;

namespace BibleMemorization.E2E.Infrastructure;

/// <summary>
/// Runs the Blazor WebAssembly dev server as a child process for the duration of
/// the test run. The dev server is used rather than a plain static file server
/// because it serves the SPA fallback, so client-side routes behave as they do
/// on GitHub Pages via 404.html.
/// </summary>
public sealed class AppHost : IAsyncDisposable
{
    private Process? _process;

    public string BaseUrl { get; private set; } = string.Empty;

    public async Task StartAsync(CancellationToken ct = default)
    {
        var port = GetFreePort();
        BaseUrl = $"http://127.0.0.1:{port}";

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = RepoLayout.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(RepoLayout.AppProject);
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add(BaseUrl);
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        // Without this the launch profile's own applicationUrl wins over --urls.
        startInfo.Environment["DOTNET_LAUNCH_PROFILE"] = "";

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the app host process.");

        // Drain the pipes. A full stdout buffer would otherwise block the child.
        var output = new List<string>();
        _ = DrainAsync(_process.StandardOutput, output);
        _ = DrainAsync(_process.StandardError, output);

        await WaitUntilReadyAsync(output, ct);
    }

    private async Task WaitUntilReadyAsync(List<string> output, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddMinutes(3);

        while (DateTime.UtcNow < deadline)
        {
            if (_process!.HasExited)
            {
                throw new InvalidOperationException(
                    $"App host exited early with code {_process.ExitCode}.{FormatOutput(output)}");
            }

            try
            {
                var response = await http.GetAsync(BaseUrl, ct);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Not listening yet.
            }
            catch (TaskCanceledException)
            {
                // Request timed out; try again.
            }

            await Task.Delay(250, ct);
        }

        throw new TimeoutException($"App host did not become ready at {BaseUrl}.{FormatOutput(output)}");
    }

    private static async Task DrainAsync(StreamReader reader, List<string> sink)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            lock (sink)
            {
                sink.Add(line);
            }
        }
    }

    private static string FormatOutput(List<string> output)
    {
        lock (output)
        {
            return output.Count == 0
                ? " (no output captured)"
                : Environment.NewLine + string.Join(Environment.NewLine, output.TakeLast(40));
        }
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        if (_process is null || _process.HasExited)
        {
            _process?.Dispose();
            return;
        }

        // dotnet run spawns the real server as a child, so kill the whole tree.
        _process.Kill(entireProcessTree: true);

        try
        {
            await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
        }
        catch (TimeoutException)
        {
            // Nothing further we can do; the process is already killed.
        }

        _process.Dispose();
    }
}
