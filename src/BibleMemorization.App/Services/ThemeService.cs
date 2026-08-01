using Microsoft.JSInterop;

namespace BibleMemorization.App.Services;

/// <summary>
/// The appearance preference, for the Settings page to read and write.
///
/// Deliberately not part of <c>AppSettings</c>: the theme has to be readable before
/// the WebAssembly runtime exists at all, so it lives in its own localStorage key and
/// is applied by the inline script in index.html before the first paint. That also
/// keeps it out of the exported save file, where it would follow a library onto a
/// device whose owner wanted something else.
/// </summary>
public sealed class ThemeService(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private readonly JsModule _module = new(jsRuntime, "./js/theme.js");

    /// <summary>The stored choice, or null when following the device.</summary>
    public ValueTask<string?> GetChoiceAsync() => _module.InvokeAsync<string?>("get");

    /// <summary>The theme actually applied right now, whatever chose it.</summary>
    public ValueTask<string> CurrentAsync() => _module.InvokeAsync<string>("current");

    /// <summary>Stores a choice and applies it. Null goes back to following the device.</summary>
    public ValueTask SetAsync(string? theme) => _module.InvokeVoidAsync("set", theme);

    public ValueTask DisposeAsync() => _module.DisposeAsync();
}
