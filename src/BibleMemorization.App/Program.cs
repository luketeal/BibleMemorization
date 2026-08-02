using BibleMemorization.App;
using BibleMemorization.App.Services;
using BibleMemorization.Core;
using BibleMemorization.Core.Bible;
using BibleMemorization.Core.Speech;
using BibleMemorization.Core.Storage;
using BibleMemorization.Core.Techniques;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Demo mode comes from the launch URL. It is registered as a factory rather than
// read here, because the query string lives on NavigationManager, which only exists
// once the host is built. Every seam below resolves lazily, so by the time any of
// them is constructed the mode is known.
builder.Services.AddSingleton(sp =>
    AppMode.FromUrl(sp.GetRequiredService<NavigationManager>().Uri));

// Techniques have no external dependencies, so they are the same in both modes.
// Order matters: the first registered becomes the default.
builder.Services.AddSingleton<IMemoryTechnique, VanishingTextTechnique>();
builder.Services.AddSingleton<IMemoryTechnique, FirstLetterTechnique>();
builder.Services.AddSingleton<TechniqueRegistry>();

// ---- Seams: one real implementation and one fake, chosen by mode ----

builder.Services.AddSingleton<IClock>(sp =>
    sp.GetRequiredService<AppMode>().IsDemo ? FixedClock.AtDefault() : new SystemClock());

builder.Services.AddSingleton<IBibleClient>(sp => sp.GetRequiredService<AppMode>().IsDemo
    ? new FakeBibleClient()
    : new HelloAoBibleClient(new HttpClient
    {
        BaseAddress = new Uri(HelloAoBibleClient.DefaultBaseAddress),
    }));

builder.Services.AddSingleton<FakeSpeechRecognizer>();
builder.Services.AddSingleton<FakeSpeechSynthesizer>();
builder.Services.AddSingleton<WebSpeechRecognizer>();
builder.Services.AddSingleton<WebSpeechSynthesizer>();

builder.Services.AddSingleton<ISpeechRecognizer>(sp => sp.GetRequiredService<AppMode>().IsDemo
    ? sp.GetRequiredService<FakeSpeechRecognizer>()
    : sp.GetRequiredService<WebSpeechRecognizer>());

builder.Services.AddSingleton<ISpeechSynthesizer>(sp => sp.GetRequiredService<AppMode>().IsDemo
    ? sp.GetRequiredService<FakeSpeechSynthesizer>()
    : sp.GetRequiredService<WebSpeechSynthesizer>());

// Demo mode is seeded and held in memory, so it opens with the same passages every
// time and never writes over what the user has stored for real.
builder.Services.AddSingleton(_ => new InMemoryStorageProvider(DemoData.Snapshot()));
builder.Services.AddSingleton<LocalStorageProvider>();
builder.Services.AddSingleton<SaveFileProvider>();

// The live backend, plus the save file, which is available in both modes: exporting
// from a demo is harmless and keeps the export path covered by tests.
builder.Services.AddSingleton<IStorageProvider>(sp => sp.GetRequiredService<AppMode>().IsDemo
    ? sp.GetRequiredService<InMemoryStorageProvider>()
    : sp.GetRequiredService<LocalStorageProvider>());
builder.Services.AddSingleton<IStorageProvider>(sp => sp.GetRequiredService<SaveFileProvider>());

// Not demo-conditional: the theme is a property of the browser, not of the library,
// so there is nothing to fake.
builder.Services.AddSingleton<ThemeService>();

builder.Services.AddSingleton<StorageProviderRegistry>();
builder.Services.AddSingleton<LibraryService>();

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
});

var host = builder.Build();

// Load the library before the first render, so pages never flash empty.
await host.Services.GetRequiredService<LibraryService>().InitializeAsync();

if (host.Services.GetRequiredService<AppMode>().IsDemo)
{
    // Test hooks exist only in demo mode, so the real app exposes no such surface.
    await TestHooks.RegisterAsync(host.Services.GetRequiredService<IJSRuntime>(), host.Services);
}

await host.RunAsync();
