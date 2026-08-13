using System;
using System.Threading;
using Avalonia;
using Avalonia.Headless;

namespace ConstructionFinance.Desktop.Tests.Finance;

/// Test-only Avalonia Application used to bootstrap the headless platform for View+ViewModel
/// tests. Subclasses the REAL production App so Initialize() still loads the real App.axaml
/// (FluentAvaloniaTheme + Theme.axaml — every DynamicResource/StaticResource the real Views
/// reference resolves exactly as it does in the running app) but overrides
/// OnFrameworkInitializationCompleted to skip the production startup work (EF Core migration
/// against the user's real SQLite database, MainWindow creation, ...) — none of which a
/// headless UI test should ever touch.
internal sealed class HeadlessTestApp : App
{
    public override void OnFrameworkInitializationCompleted()
    {
        // Deliberately empty — see class remarks. The base App's implementation would
        // migrate/seed the real user database and stand up a MainWindow.
    }
}

/// Entry point Avalonia.Headless.HeadlessUnitTestSession.StartNew looks for by convention (a
/// public static BuildAvaloniaApp() -> AppBuilder method). Real Skia is kept for rendering/
/// text shaping (UseHeadlessDrawing = false) — Headless's OWN font manager stub
/// (HeadlessFontManagerStub, wired in by UseHeadless when UseHeadlessDrawing defaults to
/// true) cannot rasterize the FluentAvaloniaUI icon font the toolbar's ui:SymbolIcon uses,
/// which otherwise throws "Could not create glyphTypeface ... Symbols" the instant a real
/// View with that toolbar is laid out. Only the WINDOWING half (dispatcher, window/clipboard/
/// input stubs) comes from Headless; Skia still does real text measurement.
internal static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HeadlessTestApp>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// Shared headless Avalonia session for every UI test in this assembly. Avalonia's headless
/// platform + Application.Current are process-global, so the session is started ONCE (lazily,
/// thread-safely) and every test dispatches its work onto its single dispatcher thread instead
/// of each starting its own.
internal static class HeadlessTestSession
{
    private static readonly Lazy<HeadlessUnitTestSession> Session = new(
        () => HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder)),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// Runs <paramref name="action"/> synchronously on the headless dispatcher thread and
    /// re-throws any exception it raised, so a test assertion failure inside surfaces as a
    /// normal xunit failure rather than a swallowed Task fault.
    public static void Run(Action action) =>
        Session.Value.Dispatch(action, CancellationToken.None).GetAwaiter().GetResult();
}
