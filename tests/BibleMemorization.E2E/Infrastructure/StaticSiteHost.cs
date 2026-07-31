using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace BibleMemorization.E2E.Infrastructure;

/// <summary>
/// Publishes the app in Release and serves the output as plain static files.
///
/// The dev server hides a whole class of bug: Release builds trim assemblies, and
/// reflection-based serialization then fails only in the deployed app. Serving the
/// real published output is the only way to catch that before GitHub Pages does.
/// </summary>
public sealed class StaticSiteHost : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _shutdown = new();
    private string _root = string.Empty;
    private Task? _loop;

    public string BaseUrl { get; private set; } = string.Empty;

    public async Task StartAsync()
    {
        _root = Path.Combine(RepoLayout.ArtifactsDir, "published", "wwwroot");
        await PublishAsync();

        var port = GetFreePort();
        BaseUrl = $"http://127.0.0.1:{port}";

        _listener.Prefixes.Add($"{BaseUrl}/");
        _listener.Start();

        _loop = Task.Run(ServeAsync);
    }

    private async Task PublishAsync()
    {
        var output = Path.Combine(RepoLayout.ArtifactsDir, "published");

        if (Directory.Exists(output))
        {
            Directory.Delete(output, recursive: true);
        }

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = RepoLayout.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add("publish");
        startInfo.ArgumentList.Add(RepoLayout.AppProject);
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(output);

        using var process = Process.Start(startInfo)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Release publish failed:{Environment.NewLine}{stdout}{stderr}");
        }
    }

    private async Task ServeAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            HttpListenerContext context;

            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception) when (_shutdown.IsCancellationRequested)
            {
                return;
            }
            catch (HttpListenerException)
            {
                return;
            }

            _ = Task.Run(() => RespondAsync(context));
        }
    }

    private async Task RespondAsync(HttpListenerContext context)
    {
        try
        {
            var relative = context.Request.Url!.AbsolutePath.TrimStart('/');

            if (relative.Length == 0)
            {
                relative = "index.html";
            }

            var path = Path.GetFullPath(Path.Combine(_root, relative));

            // Anything not on disk falls back to the app shell, which is what
            // GitHub Pages does with 404.html and what client-side routing needs.
            if (!path.StartsWith(_root, StringComparison.Ordinal) || !File.Exists(path))
            {
                path = Path.Combine(_root, "index.html");
            }

            var bytes = await File.ReadAllBytesAsync(path);

            context.Response.ContentType = ContentTypeFor(path);
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
        }
        catch
        {
            context.Response.StatusCode = 500;
        }
        finally
        {
            context.Response.Close();
        }
    }

    private static string ContentTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".js" => "text/javascript",
        ".mjs" => "text/javascript",
        ".css" => "text/css",
        ".json" => "application/json",
        ".wasm" => "application/wasm",
        ".dat" => "application/octet-stream",
        ".png" => "image/png",
        ".ico" => "image/x-icon",
        _ => "application/octet-stream",
    };

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        await _shutdown.CancelAsync();

        if (_listener.IsListening)
        {
            _listener.Stop();
        }

        _listener.Close();

        if (_loop is not null)
        {
            try
            {
                await _loop.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                // Shutting down anyway.
            }
        }

        _shutdown.Dispose();
    }
}
