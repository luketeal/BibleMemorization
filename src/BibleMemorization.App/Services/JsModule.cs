using Microsoft.JSInterop;

namespace BibleMemorization.App.Services;

/// <summary>
/// Lazily imports a JS module once and reuses it. Modules are imported on first use
/// rather than at startup so a page that never touches speech never loads it.
/// </summary>
public sealed class JsModule(IJSRuntime jsRuntime, string path) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IJSObjectReference? _module;

    public async ValueTask<IJSObjectReference> GetAsync()
    {
        if (_module is not null)
        {
            return _module;
        }

        await _gate.WaitAsync();

        try
        {
            _module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", path);
            return _module;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<T> InvokeAsync<T>(string identifier, params object?[] args)
    {
        var module = await GetAsync();
        return await module.InvokeAsync<T>(identifier, args);
    }

    public async ValueTask InvokeVoidAsync(string identifier, params object?[] args)
    {
        var module = await GetAsync();
        await module.InvokeVoidAsync(identifier, args);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The page is going away; nothing to clean up.
            }
        }

        _gate.Dispose();
    }
}
