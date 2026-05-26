# Plan 03: Avalonia Shell + Vendor CRUD UI

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dựng toàn bộ infrastructure UI cho desktop app (Avalonia 11 + DI bootstrap + MVVM patterns + database migration on startup + persistent SQLite file ở `%LocalAppData%`) và làm 1 CRUD đơn giản nhất (Vendor) như **template** cho các CRUD khác ở Plan 04. Kết thúc plan: app chạy được, mở cửa sổ chính, người dùng thêm/sửa/xóa Nhà cung cấp, dữ liệu lưu vào file `app.db`.

**Architecture:** Avalonia + MVVM strict. `Program.cs` tạo `IHost` (Microsoft.Extensions.Hosting), đăng ký Data + Application services qua `AddDataServices()`/`AddApplicationServices()` từ Plan 02, mở `MainWindow` (NavigationView chứa menu trái). Mỗi tính năng có `ViewModel` (test được, không biết Avalonia API) + `View` (XAML, code-behind tối thiểu). ViewLocator resolve View từ ViewModel theo naming convention. Dialog mở qua `IDialogService` interface (View là một implementation thực, fake/mock dùng cho test).

**Tech Stack:** .NET 8, Avalonia 11, Avalonia.Desktop, Avalonia.Themes.Fluent, FluentAvalonia (Windows 11 theme), `Avalonia.Controls.TreeDataGrid` (chưa dùng plan này, cài sẵn cho Plan 05), CommunityToolkit.Mvvm 8.x, Microsoft.Extensions.Hosting 8.x, Serilog. ViewModel tests: xUnit + Moq + Shouldly (đã có).

**Reference:** [Design doc](../specs/2026-05-19-construction-finance-app-design.md) sections 4.1 (Shell), 4.8 (Vendor module), 5.7 (storage location), [Plan 02](2026-05-20-plan-02-application-services.md) (services consumed).

**Out of scope** (Plan 04+):
- CRUD UI cho Decision/Contract/Project (Plan 04 — pattern lập từ Plan này, lặp lại)
- Cost tree UI + Payments UI (Plan 05)
- Settings UI, Backup/Restore, Inno Setup (Plan 06)
- Dashboard, Reports, Audit log viewer (sau MVP)
- Internationalization (chỉ tiếng Việt hard-coded ở plan này)

---

## Key Decisions (flag — phản hồi nếu không đồng ý)

| # | Quyết định | Lý do |
|---|---|---|
| D1 | DB file ở `Path.Combine(Environment.SpecialFolder.LocalApplicationData, "ConstructionFinance", "app.db")` | Cross-platform đúng: Windows → `%LocalAppData%`, Linux → `~/.local/share/`. Đúng convention Avalonia desktop. |
| D2 | `EnsureCreated` HAY `Database.MigrateAsync` khi startup | **Chọn `MigrateAsync`** — tôn trọng migrations đã có từ Plan 01/02, an toàn cho upgrade sau này |
| D3 | Theme mặc định **FluentAvalonia** (Windows 11 style) | Modern look, free MIT, hợp app cơ quan nhà nước |
| D4 | NavigationView của FluentAvalonia làm shell layout | Built-in collapsible sidebar, đỡ phải tự code |
| D5 | `IDialogService` interface để open dialog từ ViewModel | ViewModel không trực tiếp gọi Avalonia → testable. Implementation Avalonia mở `Window` dialog |
| D6 | Dùng Avalonia **ReactiveUI bindings** hay không? | **KHÔNG** — chỉ `CommunityToolkit.Mvvm` (đã chốt). Đơn giản hơn |
| D7 | Linux dev hỗ trợ: chạy app trên Linux để verify UI? | Có — Avalonia native Linux. Build cuối publish Win-x64 |
| D8 | ViewLocator auto-resolve View từ VM bằng naming convention (`FooViewModel` → `FooView`) | Standard Avalonia pattern. Bỏ binding XAML rườm rà |
| D9 | Setup `Serilog` log ra file `%LocalAppData%/ConstructionFinance/logs/app-{Date}.log` từ startup | Production-ready logging, dễ debug khi user báo lỗi |
| D10 | `Avalonia.Controls.DataGrid` cài như package riêng + import theme `Fluent.xaml` trong `App.axaml` | Avalonia 11 tách `DataGrid` khỏi core package; không thêm theme sẽ render trắng không header |
| D11 | `App.axaml` chỉ dùng `<sty:FluentAvaloniaTheme/>`, KHÔNG kèm `<FluentTheme/>` | Tránh duplicate resource keys giữa Avalonia built-in FluentTheme và FluentAvalonia theme |
| D12 | `IValidatable` marker interface → `DialogService` tự bind `IsPrimaryButtonEnabled` | Save button tự disable khi form invalid, không cần view biết về validation |
| D13 | `OnFrameworkInitializationCompleted` wrap try/catch + native `Window` fallback nếu fail | `async void` framework override → unhandled exception sẽ crash silent; phải catch trước khi `ContentDialog` host (MainWindow) tồn tại |

---

## File Structure

```
src/ConstructionFinance.Desktop/
├── ConstructionFinance.Desktop.csproj           (MODIFIED — add Avalonia packages, OutputType=Exe)
├── Program.cs                                    (NEW — entry point, BuildAvaloniaApp + IHost)
├── App.axaml                                     (NEW — application resources, theme)
├── App.axaml.cs                                  (NEW — OnFrameworkInitializationCompleted)
├── ViewLocator.cs                                (NEW — IDataTemplate, VM→View resolution)
├── app.manifest                                  (NEW — Windows manifest, DPI awareness)
├── Common/
│   ├── AppPaths.cs                               (NEW — LocalAppData paths)
│   ├── HostBuilder.cs                            (NEW — IHost setup)
│   └── ViewModelBase.cs                          (NEW — base for all VMs)
├── Services/
│   ├── IDialogService.cs                         (NEW — abstraction)
│   ├── DialogService.cs                          (NEW — Avalonia impl)
│   ├── INavigationService.cs                     (NEW — abstraction)
│   └── NavigationService.cs                      (NEW — switches content)
├── Views/
│   ├── MainWindow.axaml                          (NEW — shell)
│   ├── MainWindow.axaml.cs
│   ├── Vendors/
│   │   ├── VendorListView.axaml
│   │   ├── VendorListView.axaml.cs
│   │   ├── VendorEditDialog.axaml
│   │   └── VendorEditDialog.axaml.cs
│   └── Common/
│       └── ConfirmDialog.axaml                   (NEW — reusable yes/no)
│       └── ConfirmDialog.axaml.cs
├── ViewModels/
│   ├── MainWindowViewModel.cs                    (NEW — navigation state, status bar)
│   ├── Vendors/
│   │   ├── VendorListViewModel.cs
│   │   └── VendorEditDialogViewModel.cs
│   └── Common/
│       └── ConfirmDialogViewModel.cs
└── Assets/
    └── icon.ico                                  (placeholder — replaced later)

tests/ConstructionFinance.Desktop.Tests/          (NEW PROJECT — ViewModel-only tests)
├── ConstructionFinance.Desktop.Tests.csproj
├── Vendors/
│   ├── VendorListViewModelTests.cs
│   └── VendorEditDialogViewModelTests.cs
└── Common/
    └── FakeDialogService.cs                      (test double)
```

---

## Pre-flight

- [ ] **Step 0.1: Verify Plans 01+02 are committed and green**

Run from `/home/hieu-nguyen/management/`:
```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
git status --short
dotnet build --configuration Release
dotnet test --configuration Release
```
Expected: Clean working tree (commit any pending Plan 02 changes first). Build 0 errors. All 114 existing tests pass.

If working tree dirty: commit pending Plan 02 changes BEFORE starting Plan 03. (User decision — ask if uncertain.)

- [ ] **Step 0.2: Verify Linux desktop runtime libs (for `dotnet run` UI testing)**

Run:
```bash
ldconfig -p | grep -E "libfontconfig|libice|libsm|libx11" | head -8
```
Expected: At least 1 line per lib. If missing, install:
```bash
sudo apt-get install -y libfontconfig1 libice6 libsm6 libx11-6
```

---

## Task 1: Add Avalonia packages + central versions + convert Desktop to Exe

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/ConstructionFinance.Desktop/ConstructionFinance.Desktop.csproj`

- [ ] **Step 1.1: Add Avalonia package versions to `Directory.Packages.props`**

Edit `/home/hieu-nguyen/management/Directory.Packages.props`. Add inside the `<ItemGroup>`:
```xml
    <!-- Avalonia 11 -->
    <PackageVersion Include="Avalonia" Version="11.2.1" />
    <PackageVersion Include="Avalonia.Desktop" Version="11.2.1" />
    <PackageVersion Include="Avalonia.Themes.Fluent" Version="11.2.1" />
    <PackageVersion Include="Avalonia.Fonts.Inter" Version="11.2.1" />
    <PackageVersion Include="Avalonia.Diagnostics" Version="11.2.1" />
    <PackageVersion Include="Avalonia.Controls.DataGrid" Version="11.2.1" />
    <PackageVersion Include="Avalonia.Controls.TreeDataGrid" Version="11.0.10" />
    <PackageVersion Include="FluentAvaloniaUI" Version="2.2.0" />
```

> ⚠️ Intentionally NOT adding `Avalonia.ReactiveUI` — per **D6** we use `CommunityToolkit.Mvvm` only. Adding ReactiveUI would pull in a second MVVM stack and risk subtle conflicts.

- [ ] **Step 1.2: Convert Desktop project to executable + add Avalonia references**

Replace the contents of `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ConstructionFinance.Desktop.csproj` with:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <RootNamespace>ConstructionFinance.Desktop</RootNamespace>
    <AssemblyName>ConstructionFinance</AssemblyName>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" />
    <PackageReference Include="Avalonia.Desktop" />
    <PackageReference Include="Avalonia.Themes.Fluent" />
    <PackageReference Include="Avalonia.Fonts.Inter" />
    <PackageReference Include="Avalonia.Controls.DataGrid" />
    <PackageReference Include="Avalonia.Controls.TreeDataGrid" />
    <PackageReference Include="FluentAvaloniaUI" />
    <PackageReference Include="CommunityToolkit.Mvvm" />
    <PackageReference Include="Microsoft.Extensions.Hosting" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" />
    <PackageReference Include="Serilog" />
    <PackageReference Include="Serilog.Extensions.Hosting" />
    <PackageReference Include="Serilog.Sinks.File" />
    <PackageReference Include="Serilog.Sinks.Console" />
    <!-- Diagnostics ONLY in Debug -->
    <PackageReference Include="Avalonia.Diagnostics" Condition="'$(Configuration)' == 'Debug'" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\ConstructionFinance.Application\ConstructionFinance.Application.csproj" />
    <ProjectReference Include="..\ConstructionFinance.Data\ConstructionFinance.Data.csproj" />
    <ProjectReference Include="..\ConstructionFinance.Domain\ConstructionFinance.Domain.csproj" />
    <ProjectReference Include="..\ConstructionFinance.Infrastructure\ConstructionFinance.Infrastructure.csproj" />
  </ItemGroup>

  <ItemGroup>
    <AvaloniaResource Include="Assets\**" />
  </ItemGroup>
</Project>
```

- [ ] **Step 1.3: Verify CommunityToolkit.Mvvm + Microsoft.Extensions.Hosting versions present**

The packages must already exist in `Directory.Packages.props` from earlier plans. Check:
```bash
grep -E "CommunityToolkit.Mvvm|Microsoft.Extensions.Hosting" Directory.Packages.props
```
Expected: Both lines present (Plan 01 added them). If missing, add to the `<ItemGroup>`:
```xml
    <PackageVersion Include="CommunityToolkit.Mvvm" Version="8.4.0" />
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="8.0.1" />
```

- [ ] **Step 1.4: Restore + build**

Run: `dotnet restore && dotnet build`
Expected: Build succeeds (warnings about Avalonia EntryPoint missing — fixed in next task).

- [ ] **Step 1.5: Commit**

```bash
git add Directory.Packages.props src/ConstructionFinance.Desktop/ConstructionFinance.Desktop.csproj
git commit -m "chore(desktop): add Avalonia 11 + FluentAvalonia + Hosting packages, convert to WinExe"
```

---

## Task 2: Application bootstrap — Program.cs + App.axaml + AppPaths

**Files:**
- Create: `src/ConstructionFinance.Desktop/Program.cs`
- Create: `src/ConstructionFinance.Desktop/App.axaml`
- Create: `src/ConstructionFinance.Desktop/App.axaml.cs`
- Create: `src/ConstructionFinance.Desktop/app.manifest`
- Create: `src/ConstructionFinance.Desktop/Common/AppPaths.cs`

- [ ] **Step 2.1: Create Windows manifest (DPI awareness, UAC)**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/app.manifest`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="ConstructionFinance.Desktop"/>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
    <security>
      <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
        <requestedExecutionLevel level="asInvoker" uiAccess="false"/>
      </requestedPrivileges>
    </security>
  </trustInfo>
</assembly>
```

- [ ] **Step 2.2: Create AppPaths helper**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Common/AppPaths.cs`:
```csharp
namespace ConstructionFinance.Desktop.Common;

public static class AppPaths
{
    public const string AppFolderName = "ConstructionFinance";

    public static string LocalAppDataRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppFolderName);

    public static string DatabaseFile => Path.Combine(LocalAppDataRoot, "app.db");
    public static string AttachmentsFolder => Path.Combine(LocalAppDataRoot, "attachments");
    public static string LogsFolder => Path.Combine(LocalAppDataRoot, "logs");
    public static string BackupsFolder => Path.Combine(LocalAppDataRoot, "Backups");

    public static string ConnectionString =>
        $"DataSource={DatabaseFile};Cache=Shared;Foreign Keys=True";

    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(LocalAppDataRoot);
        Directory.CreateDirectory(AttachmentsFolder);
        Directory.CreateDirectory(LogsFolder);
        Directory.CreateDirectory(BackupsFolder);
    }
}
```

- [ ] **Step 2.3: Create App.axaml**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/App.axaml`:
```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:sty="using:FluentAvalonia.Styling"
             xmlns:local="using:ConstructionFinance.Desktop"
             x:Class="ConstructionFinance.Desktop.App"
             RequestedThemeVariant="Light">
    <Application.DataTemplates>
        <local:ViewLocator/>
    </Application.DataTemplates>
    <Application.Styles>
        <!-- FluentAvalonia provides the base Fluent theme; do NOT combine with Avalonia <FluentTheme/> -->
        <sty:FluentAvaloniaTheme PreferUserAccentColor="True"/>
        <!-- DataGrid theme (in its own package since Avalonia 11) -->
        <StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"/>
    </Application.Styles>
</Application>
```

> Why no `<FluentTheme/>`: FluentAvalonia 2.x ships its own theme that REPLACES Avalonia's built-in FluentTheme. Including both causes duplicate resource keys and silent style overrides.

- [ ] **Step 2.4: Create App.axaml.cs (stub — Host wired in Task 3)**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/App.axaml.cs`:
```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // MainWindow assigned in Task 3 (via HostBuilder DI).
        }
        base.OnFrameworkInitializationCompleted();
    }
}
```

- [ ] **Step 2.5: Create Program.cs entry point**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Program.cs`:
```csharp
using Avalonia;

namespace ConstructionFinance.Desktop;

internal static class Program
{
    // STAThread required by Avalonia + Windows COM for clipboard/file dialogs.
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
```

> No `using Avalonia.ReactiveUI;` and no `UseReactiveUI()` call — per **D6**, MVVM stack is CommunityToolkit only.

- [ ] **Step 2.6: Build + try running (will show blank window)**

Run:
```bash
dotnet build src/ConstructionFinance.Desktop/
dotnet run --project src/ConstructionFinance.Desktop/ &
sleep 3
pkill -f "dotnet.*ConstructionFinance.Desktop" || true
```
Expected: A blank Avalonia window appears briefly. Build succeeds. If `dotnet run` errors complaining about display: skip on headless servers — verify build only.

- [ ] **Step 2.7: Commit**

```bash
git add src/ConstructionFinance.Desktop/
git commit -m "feat(desktop): Avalonia bootstrap (Program.cs, App.axaml, app.manifest, AppPaths)"
```

---

## Task 3: ViewLocator + ViewModelBase + Host with DI

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewLocator.cs`
- Create: `src/ConstructionFinance.Desktop/Common/ViewModelBase.cs`
- Create: `src/ConstructionFinance.Desktop/Common/HostBuilder.cs`
- Modify: `src/ConstructionFinance.Desktop/App.axaml.cs`

The `ViewLocator` matches `XxxViewModel` to `XxxView` by namespace replacement: `ConstructionFinance.Desktop.ViewModels.Vendors.VendorListViewModel` → `ConstructionFinance.Desktop.Views.Vendors.VendorListView`.

- [ ] **Step 3.1: Create ViewLocator**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewLocator.cs`:
```csharp
using System.Collections.Concurrent;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.ViewModels;

namespace ConstructionFinance.Desktop;

public class ViewLocator : IDataTemplate
{
    // Cache resolved VM->View types so repeated bindings don't repeat reflection.
    private static readonly ConcurrentDictionary<Type, Type?> _cache = new();

    public Control? Build(object? data)
    {
        if (data is null) return null;
        var vmType = data.GetType();
        var viewType = _cache.GetOrAdd(vmType, Resolve);
        if (viewType is null)
            return new TextBlock { Text = $"View not found for {vmType.FullName}" };
        return (Control)Activator.CreateInstance(viewType)!;
    }

    // Match every ViewModelBase EXCEPT MainWindowViewModel — MainWindow is a Window, not
    // a DataTemplate child. Returning a Window from a DataTemplate causes runtime errors.
    public bool Match(object? data) => data is ViewModelBase and not MainWindowViewModel;

    private static Type? Resolve(Type vmType)
    {
        var fullName = vmType.FullName;
        if (fullName is null) return null;

        // Candidate A: ViewModels.X.YViewModel -> Views.X.YView   (UserControls, list pages)
        var candidateA = fullName
            .Replace(".ViewModels.", ".Views.", StringComparison.Ordinal);
        if (candidateA.EndsWith("ViewModel", StringComparison.Ordinal))
            candidateA = candidateA[..^"ViewModel".Length] + "View";

        // Candidate B: ViewModels.X.YDialogViewModel -> Views.X.YDialog   (already named *Dialog)
        var candidateB = fullName
            .Replace(".ViewModels.", ".Views.", StringComparison.Ordinal);
        if (candidateB.EndsWith("ViewModel", StringComparison.Ordinal))
            candidateB = candidateB[..^"ViewModel".Length];

        var assembly = vmType.Assembly;
        var resolved = assembly.GetType(candidateA) ?? assembly.GetType(candidateB);

        // Defensive: never return a Window from a DataTemplate.
        if (resolved is not null && typeof(Window).IsAssignableFrom(resolved))
            return null;

        return resolved;
    }
}
```

Why these tweaks:
- Use `assembly.GetType(name)` (instead of `Type.GetType(name)`) so the lookup works even when the desktop assembly isn't the entry assembly (e.g. when unit-testing the locator).
- Cache results with `ConcurrentDictionary` — `Build` is called once per DataTemplate render which can be on the hot path.
- Hard-block `Window` types — protects against the silent bug where `MainWindowViewModel` gets bound to a `ContentControl`.
- `Match` explicitly excludes `MainWindowViewModel` so it never even attempts a lookup.

- [ ] **Step 3.2: Create ViewModelBase**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Common/ViewModelBase.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace ConstructionFinance.Desktop.Common;

public abstract class ViewModelBase : ObservableObject { }
```

- [ ] **Step 3.3: Create HostBuilder helper (wires Microsoft.Extensions.Hosting + Serilog + services)**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Common/HostBuilder.cs`:
```csharp
using ConstructionFinance.Application;
using ConstructionFinance.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace ConstructionFinance.Desktop.Common;

public static class HostBuilder
{
    public static IHost Build()
    {
        AppPaths.EnsureDirectoriesExist();

#if DEBUG
        const LogEventLevel minLevel = LogEventLevel.Debug;
#else
        const LogEventLevel minLevel = LogEventLevel.Information;
#endif

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(minLevel)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                path: Path.Combine(AppPaths.LogsFolder, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        return Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((_, services) =>
            {
                services.AddDbContext<AppDbContext>(opt =>
                    opt.UseSqlite(AppPaths.ConnectionString));
                services.AddDataServices();
                services.AddApplicationServices();
                // ViewModels + IDialogService/INavigationService registered in later tasks.
            })
            .Build();
    }
}
```

Why these tweaks:
- `#if DEBUG` so we get verbose logs locally but a quieter production runtime.
- Override EF Core to Warning — otherwise every query/SaveChanges floods the log file.
- `Enrich.FromLogContext()` + structured output template makes log files searchable (timestamp, level, SourceContext).

- [ ] **Step 3.4: Update App.axaml.cs to use HostBuilder + run migrations on startup**

Replace `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/App.axaml.cs` with:
```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ConstructionFinance.Data;
using ConstructionFinance.Desktop.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ConstructionFinance.Desktop;

public partial class App : Application
{
    public static IHost? Host { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    // `async void` is acceptable here ONLY because it's an Avalonia framework override.
    // ALL awaits must be wrapped in try/catch — unhandled exceptions here crash silently.
    public override async void OnFrameworkInitializationCompleted()
    {
        try
        {
            Host = Common.HostBuilder.Build();
            Log.Information("App starting. DB={DbPath}", AppPaths.DatabaseFile);

            using (var scope = Host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync();
                ConstructionFinance.Data.Seed.CostUnitTypeSeeder.Seed(db);
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // MainWindow is assigned in Task 4 once the VM is registered.
                desktop.ShutdownRequested += (_, _) =>
                {
                    Log.Information("App shutting down.");
                    Host?.Dispose();
                    Log.CloseAndFlush();
                };
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fatal error during startup");
            ShowFatalError(ex);
            // Force shutdown so user is not stuck on a black window.
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
                d.Shutdown(1);
        }
        finally
        {
            base.OnFrameworkInitializationCompleted();
        }
    }

    private static void ShowFatalError(Exception ex)
    {
        // Fallback to a native message box via Avalonia's UI thread.
        // We cannot use FluentAvalonia ContentDialog here because the main window may not exist yet.
        Dispatcher.UIThread.Post(() =>
        {
            var window = new Window
            {
                Title = "Lỗi nghiêm trọng",
                Width = 520, Height = 280,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = new ScrollViewer
                {
                    Padding = new Avalonia.Thickness(16),
                    Content = new TextBlock
                    {
                        Text = $"Ứng dụng không thể khởi động.\n\nChi tiết:\n{ex.GetType().Name}: {ex.Message}\n\nXem log tại:\n{AppPaths.LogsFolder}",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    }
                }
            };
            window.Show();
        });
    }
}
```

Why these changes:
- Wrap the entire startup sequence in `try/catch` — `MigrateAsync` can fail (DB locked, schema drift, disk full, perms). Without this, the app crashes invisibly.
- Log every transition with Serilog before falling back to a native `Window` message.
- Fallback uses bare `Window` because FluentAvalonia `ContentDialog` requires a `TopLevel`/`MainWindow` to host it — which doesn't exist yet at this point.

- [ ] **Step 3.5: Build**

Run: `dotnet build src/ConstructionFinance.Desktop/`
Expected: 0 errors.

- [ ] **Step 3.6: Commit**

```bash
git add src/ConstructionFinance.Desktop/
git commit -m "feat(desktop): ViewLocator + ViewModelBase + Host with DI, Serilog, EF migrations"
```

---

## Task 4: MainWindow shell + MainWindowViewModel

**Files:**
- Create: `src/ConstructionFinance.Desktop/Views/MainWindow.axaml`
- Create: `src/ConstructionFinance.Desktop/Views/MainWindow.axaml.cs`
- Create: `src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/App.axaml.cs`
- Modify: `src/ConstructionFinance.Desktop/Common/HostBuilder.cs`

- [ ] **Step 4.1: Create MainWindowViewModel (just title + status text for now)**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private string _title = "Quản lý Tài chính Công trình";
    [ObservableProperty] private string _statusText = $"DB: {AppPaths.DatabaseFile} | Sẵn sàng";
    [ObservableProperty] private object? _currentContent;
}
```

- [ ] **Step 4.2: Create MainWindow.axaml (NavigationView shell)**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/MainWindow.axaml`:
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:ConstructionFinance.Desktop.ViewModels"
        xmlns:ui="using:FluentAvalonia.UI.Controls"
        x:Class="ConstructionFinance.Desktop.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Title="{Binding Title}"
        Width="1280" Height="800"
        MinWidth="900" MinHeight="600"
        WindowStartupLocation="CenterScreen">
    <DockPanel>
        <Border DockPanel.Dock="Bottom" Background="#F3F3F3" Padding="10,4">
            <TextBlock Text="{Binding StatusText}" FontSize="12"/>
        </Border>
        <ui:NavigationView x:Name="Nav"
                           PaneDisplayMode="Left"
                           IsPaneToggleButtonVisible="True"
                           IsSettingsVisible="False"
                           IsBackButtonVisible="False"
                           OpenPaneLength="220"
                           CompactPaneLength="48">
            <ui:NavigationView.MenuItems>
                <ui:NavigationViewItem Content="Trang chủ" Tag="Home">
                    <ui:NavigationViewItem.IconSource><ui:SymbolIconSource Symbol="Home"/></ui:NavigationViewItem.IconSource>
                </ui:NavigationViewItem>
                <ui:NavigationViewItem Content="Quyết định" Tag="Decisions">
                    <ui:NavigationViewItem.IconSource><ui:SymbolIconSource Symbol="Document"/></ui:NavigationViewItem.IconSource>
                </ui:NavigationViewItem>
                <ui:NavigationViewItem Content="Cây Dự án" Tag="Projects">
                    <ui:NavigationViewItem.IconSource><ui:SymbolIconSource Symbol="Folder"/></ui:NavigationViewItem.IconSource>
                </ui:NavigationViewItem>
                <ui:NavigationViewItem Content="Nhà cung cấp" Tag="Vendors">
                    <ui:NavigationViewItem.IconSource><ui:SymbolIconSource Symbol="People"/></ui:NavigationViewItem.IconSource>
                </ui:NavigationViewItem>
                <ui:NavigationViewItem Content="Báo cáo" Tag="Reports">
                    <ui:NavigationViewItem.IconSource><ui:SymbolIconSource Symbol="PreviewLink"/></ui:NavigationViewItem.IconSource>
                </ui:NavigationViewItem>
                <ui:NavigationViewItem Content="Lịch sử" Tag="Audit">
                    <ui:NavigationViewItem.IconSource><ui:SymbolIconSource Symbol="Clock"/></ui:NavigationViewItem.IconSource>
                </ui:NavigationViewItem>
                <ui:NavigationViewItem Content="Cài đặt" Tag="Settings">
                    <ui:NavigationViewItem.IconSource><ui:SymbolIconSource Symbol="Setting"/></ui:NavigationViewItem.IconSource>
                </ui:NavigationViewItem>
            </ui:NavigationView.MenuItems>
            <ContentControl Content="{Binding CurrentContent}" Margin="16"/>
        </ui:NavigationView>
    </DockPanel>
</Window>
```

- [ ] **Step 4.3: Create MainWindow.axaml.cs**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/MainWindow.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 4.4: Register MainWindowViewModel in HostBuilder**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Common/HostBuilder.cs`. Inside the `ConfigureServices` lambda (after `AddApplicationServices`), add:
```csharp
                services.AddSingleton<ViewModels.MainWindowViewModel>();
```
Also add the using at the top:
```csharp
using ConstructionFinance.Desktop.ViewModels;
```
(or rely on fully-qualified `ConstructionFinance.Desktop.ViewModels.MainWindowViewModel`).

- [ ] **Step 4.5: Wire MainWindow in App.axaml.cs**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/App.axaml.cs`. Inside the `try` block (defined in Step 3.4), replace the existing `if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)` block with the version below — it now resolves `MainWindowViewModel` from DI and assigns `desktop.MainWindow`. The surrounding try/catch from Step 3.4 stays exactly as-is:
```csharp
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var vm = Host!.Services.GetRequiredService<ViewModels.MainWindowViewModel>();
                desktop.MainWindow = new Views.MainWindow { DataContext = vm };
                desktop.ShutdownRequested += (_, _) =>
                {
                    Log.Information("App shutting down.");
                    Host?.Dispose();
                    Log.CloseAndFlush();
                };
            }
```

- [ ] **Step 4.6: Build + try running**

Run:
```bash
dotnet build src/ConstructionFinance.Desktop/
dotnet run --project src/ConstructionFinance.Desktop/ &
sleep 4
pkill -f "dotnet.*ConstructionFinance.Desktop" || true
ls -la "$HOME/.local/share/ConstructionFinance/" 2>/dev/null
```
Expected:
- Build succeeds
- App window opens with sidebar menu (Home, Decisions, Projects, Vendors, Reports, Audit, Settings)
- `$HOME/.local/share/ConstructionFinance/app.db` is created (migrations applied)
- Logs folder created

If running headless: skip the visual check, just verify the .db file appears.

- [ ] **Step 4.7: Commit**

```bash
git add .
git commit -m "feat(desktop): MainWindow shell with FluentAvalonia NavigationView + migrations on startup"
```

---

## Task 5: IDialogService abstraction + ConfirmDialog reusable

**Files:**
- Create: `src/ConstructionFinance.Desktop/Services/IDialogService.cs`
- Create: `src/ConstructionFinance.Desktop/Services/DialogService.cs`
- Create: `src/ConstructionFinance.Desktop/ViewModels/Common/ConfirmDialogViewModel.cs`
- Create: `src/ConstructionFinance.Desktop/Views/Common/ConfirmDialog.axaml`
- Create: `src/ConstructionFinance.Desktop/Views/Common/ConfirmDialog.axaml.cs`

- [ ] **Step 5.1: Define IDialogService + marker interfaces**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Services/IDialogService.cs`:
```csharp
using System.ComponentModel;

namespace ConstructionFinance.Desktop.Services;

public interface IDialogService
{
    /// Open a content dialog whose body is the View resolved from <paramref name="viewModel"/> via ViewLocator.
    /// Returns the dialog result — `true` if user confirmed, `false`/`null` if cancelled.
    Task<bool?> ShowDialogAsync<TViewModel>(TViewModel viewModel) where TViewModel : class;

    /// Show a simple Yes/No confirmation. Returns true if user confirms.
    Task<bool> ConfirmAsync(string title, string message);

    /// Show a message dialog (info/error).
    Task ShowMessageAsync(string title, string message);
}

/// Marker for dialog VMs that expose a dynamic title.
public interface IHasDialogTitle
{
    string DialogTitle { get; }
}

/// Marker for dialog VMs that expose validation state.
/// `DialogService` will bind the primary button's IsEnabled to <see cref="IsValid"/>
/// so users cannot submit invalid forms.
public interface IValidatable : INotifyPropertyChanged
{
    bool IsValid { get; }
}
```

- [ ] **Step 5.2: Create ConfirmDialogViewModel**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Common/ConfirmDialogViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Common;

public partial class ConfirmDialogViewModel : ViewModelBase
{
    [ObservableProperty] private string _title = "Xác nhận";
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private string _confirmText = "Đồng ý";
    [ObservableProperty] private string _cancelText = "Hủy";
}
```

- [ ] **Step 5.3: Create ConfirmDialog.axaml + code-behind**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Common/ConfirmDialog.axaml`:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Common"
             x:Class="ConstructionFinance.Desktop.Views.Common.ConfirmDialog"
             x:DataType="vm:ConfirmDialogViewModel"
             MinWidth="380" Padding="16">
    <StackPanel Spacing="12">
        <TextBlock Text="{Binding Message}" TextWrapping="Wrap"/>
    </StackPanel>
</UserControl>
```

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Common/ConfirmDialog.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Common;

public partial class ConfirmDialog : UserControl
{
    public ConfirmDialog() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 5.4: Implement DialogService using FluentAvalonia ContentDialog**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Services/DialogService.cs`:
```csharp
using System.ComponentModel;
using Avalonia.Controls;
using ConstructionFinance.Desktop.ViewModels.Common;
using FluentAvalonia.UI.Controls;

namespace ConstructionFinance.Desktop.Services;

public class DialogService : IDialogService
{
    private static readonly ViewLocator _locator = new();

    public async Task<bool?> ShowDialogAsync<TViewModel>(TViewModel viewModel) where TViewModel : class
    {
        var content = _locator.Build(viewModel) ?? new TextBlock { Text = "(no view)" };
        content.DataContext = viewModel;

        var dialog = new ContentDialog
        {
            Title = (viewModel as IHasDialogTitle)?.DialogTitle ?? string.Empty,
            Content = content,
            PrimaryButtonText = "Lưu",
            CloseButtonText = "Hủy",
            DefaultButton = ContentDialogButton.Primary
        };

        // If the VM implements IValidatable, gate the primary (Save) button on IsValid so
        // the user cannot submit an invalid form. Subscribe + initial sync + unsubscribe on close.
        if (viewModel is IValidatable validatable)
        {
            dialog.IsPrimaryButtonEnabled = validatable.IsValid;
            void OnPropChanged(object? s, PropertyChangedEventArgs e)
            {
                if (e.PropertyName is nameof(IValidatable.IsValid) or null)
                    dialog.IsPrimaryButtonEnabled = validatable.IsValid;
            }
            validatable.PropertyChanged += OnPropChanged;
            dialog.Closed += (_, _) => validatable.PropertyChanged -= OnPropChanged;
        }

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var vm = new ConfirmDialogViewModel { Title = title, Message = message };
        var content = new Views.Common.ConfirmDialog { DataContext = vm };
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = vm.ConfirmText,
            CloseButtonText = vm.CancelText,
            DefaultButton = ContentDialogButton.Close  // Safe default: cancel
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            CloseButtonText = "Đóng"
        };
        await dialog.ShowAsync();
    }
}
```

Why these changes:
- `IHasDialogTitle` lifted to `IDialogService.cs` so DTO/contract live together.
- Primary button auto-disables for invalid forms via `IValidatable`. The subscription is cleaned up on `dialog.Closed`.
- `ConfirmAsync` defaults to `Close` (cancel) — safer for destructive prompts: pressing Enter doesn't accidentally confirm a delete.
- ViewLocator is now reused as a single static instance (cache survives across dialogs).

- [ ] **Step 5.5: Register DialogService in HostBuilder**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Common/HostBuilder.cs`. Inside `ConfigureServices`, after the ViewModel registration line, add:
```csharp
                services.AddSingleton<Services.IDialogService, Services.DialogService>();
```

- [ ] **Step 5.6: Build**

Run: `dotnet build src/ConstructionFinance.Desktop/`
Expected: 0 errors.

- [ ] **Step 5.7: Commit**

```bash
git add .
git commit -m "feat(desktop): IDialogService + DialogService (ContentDialog wrapper) + ConfirmDialog"
```

---

## Task 6: Create Desktop.Tests project + FakeDialogService

**Files:**
- New test project: `tests/ConstructionFinance.Desktop.Tests/`
- Create: `tests/ConstructionFinance.Desktop.Tests/Common/FakeDialogService.cs`

ViewModel tests run headless (no Avalonia UI rendered). They construct VM with mock services + FakeDialogService and assert state/commands.

- [ ] **Step 6.1: Create xunit test project + add to solution**

Run from `/home/hieu-nguyen/management/`:
```bash
cd tests
dotnet new xunit -n ConstructionFinance.Desktop.Tests -o ConstructionFinance.Desktop.Tests
cd ..
rm -f tests/ConstructionFinance.Desktop.Tests/UnitTest1.cs
dotnet sln add tests/ConstructionFinance.Desktop.Tests/ConstructionFinance.Desktop.Tests.csproj
dotnet add tests/ConstructionFinance.Desktop.Tests/ConstructionFinance.Desktop.Tests.csproj reference src/ConstructionFinance.Desktop/ConstructionFinance.Desktop.csproj
dotnet add tests/ConstructionFinance.Desktop.Tests/ConstructionFinance.Desktop.Tests.csproj reference src/ConstructionFinance.Application/ConstructionFinance.Application.csproj
dotnet add tests/ConstructionFinance.Desktop.Tests/ConstructionFinance.Desktop.Tests.csproj reference src/ConstructionFinance.Domain/ConstructionFinance.Domain.csproj
```

- [ ] **Step 6.2: Set test packages**

Replace the `<ItemGroup>` containing test packages in `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/ConstructionFinance.Desktop.Tests.csproj` with:
```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Shouldly" />
    <PackageReference Include="Moq" />
    <PackageReference Include="coverlet.collector" />
  </ItemGroup>
```

- [ ] **Step 6.3: Create FakeDialogService**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/Common/FakeDialogService.cs`:
```csharp
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.Tests.Common;

public class FakeDialogService : IDialogService
{
    public List<object> OpenedDialogs { get; } = new();
    public List<string> ShownMessages { get; } = new();
    public bool ConfirmResult { get; set; } = true;
    public bool? DialogResult { get; set; } = true;

    public Task<bool?> ShowDialogAsync<TViewModel>(TViewModel viewModel) where TViewModel : class
    {
        OpenedDialogs.Add(viewModel);
        return Task.FromResult(DialogResult);
    }

    public Task<bool> ConfirmAsync(string title, string message)
    {
        ShownMessages.Add($"CONFIRM: {title} — {message}");
        return Task.FromResult(ConfirmResult);
    }

    public Task ShowMessageAsync(string title, string message)
    {
        ShownMessages.Add($"MSG: {title} — {message}");
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 6.4: Build + test (no tests yet, just verify project wires up)**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/`
Expected: "Total tests: 0".

- [ ] **Step 6.5: Commit**

```bash
git add .
git commit -m "test(desktop): create Desktop.Tests project + FakeDialogService for VM tests"
```

---

## Task 7: VendorListViewModel — TDD

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/Vendors/VendorListViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Vendors/VendorListViewModelTests.cs`

- [ ] **Step 7.1: Write failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/Vendors/VendorListViewModelTests.cs`:
```csharp
using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Vendors;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Vendors;

public class VendorListViewModelTests
{
    private static VendorDto V(int id, string name) =>
        new(id, null, name, null, null, null, null, null, null);

    private static (VendorListViewModel vm, Mock<IVendorService> svc, FakeDialogService dlg) Create(IEnumerable<VendorDto>? initial = null)
    {
        var svc = new Mock<IVendorService>(MockBehavior.Strict);
        svc.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((initial ?? Array.Empty<VendorDto>()).ToList());
        var dlg = new FakeDialogService();
        var vm = new VendorListViewModel(svc.Object, dlg);
        return (vm, svc, dlg);
    }

    [Fact]
    public async Task LoadAsync_populates_Vendors()
    {
        var (vm, _, _) = Create(new[] { V(1, "Alpha"), V(2, "Beta") });
        await vm.LoadAsync();

        vm.Vendors.Count.ShouldBe(2);
        vm.Vendors[0].VendorName.ShouldBe("Alpha");
    }

    [Fact]
    public async Task AddCommand_opens_dialog_and_calls_CreateAsync_on_success()
    {
        var (vm, svc, dlg) = Create();
        svc.Setup(s => s.CreateAsync(It.IsAny<CreateVendorDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(V(99, "New"));
        dlg.DialogResult = true;
        await vm.LoadAsync();

        await vm.AddCommand.ExecuteAsync(null);

        dlg.OpenedDialogs.Count.ShouldBe(1);
        svc.Verify(s => s.CreateAsync(It.IsAny<CreateVendorDto>(), It.IsAny<CancellationToken>()), Times.Once);
        vm.Vendors.ShouldContain(v => v.VendorName == "New");
    }

    [Fact]
    public async Task AddCommand_when_user_cancels_dialog_does_not_call_CreateAsync()
    {
        var (vm, svc, dlg) = Create();
        dlg.DialogResult = false;
        await vm.LoadAsync();

        await vm.AddCommand.ExecuteAsync(null);

        svc.Verify(s => s.CreateAsync(It.IsAny<CreateVendorDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteCommand_with_confirm_calls_DeleteAsync_and_removes_from_list()
    {
        var (vm, svc, dlg) = Create(new[] { V(1, "Alpha") });
        svc.Setup(s => s.DeleteAsync(1, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        dlg.ConfirmResult = true;
        await vm.LoadAsync();
        vm.SelectedVendor = vm.Vendors[0];

        await vm.DeleteCommand.ExecuteAsync(null);

        svc.Verify(s => s.DeleteAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        vm.Vendors.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteCommand_when_user_declines_confirm_does_nothing()
    {
        var (vm, svc, dlg) = Create(new[] { V(1, "Alpha") });
        dlg.ConfirmResult = false;
        await vm.LoadAsync();
        vm.SelectedVendor = vm.Vendors[0];

        await vm.DeleteCommand.ExecuteAsync(null);

        svc.Verify(s => s.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        vm.Vendors.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DeleteCommand_disabled_when_no_selection()
    {
        var (vm, _, _) = Create(new[] { V(1, "Alpha") });
        await vm.LoadAsync();
        vm.SelectedVendor = null;

        vm.DeleteCommand.CanExecute(null).ShouldBeFalse();
    }
}
```

- [ ] **Step 7.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~VendorListViewModelTests"`
Expected: Type `VendorListViewModel` not found.

- [ ] **Step 7.3: Implement VendorListViewModel**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Vendors/VendorListViewModel.cs`:
```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Vendors;

public partial class VendorListViewModel : ViewModelBase
{
    private readonly IVendorService _service;
    private readonly IDialogService _dialog;

    public VendorListViewModel(IVendorService service, IDialogService dialog)
    {
        _service = service;
        _dialog = dialog;
    }

    public ObservableCollection<VendorDto> Vendors { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private VendorDto? _selectedVendor;

    // IsBusy gates every command so spam-clicks during async work don't double-submit.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private bool _isBusy;

    [ObservableProperty] private string? _errorMessage;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var list = await _service.GetAllAsync(ct);
            Vendors.Clear();
            foreach (var v in list) Vendors.Add(v);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task AddAsync()
    {
        var editVm = new VendorEditDialogViewModel(initial: null);
        var ok = await _dialog.ShowDialogAsync(editVm);
        if (ok != true) return;
        await RunAsync(async () =>
        {
            var created = await _service.CreateAsync(editVm.ToCreateDto());
            Vendors.Add(created);
        });
    }

    [RelayCommand(CanExecute = nameof(CanInteractOnSelection))]
    private async Task EditAsync()
    {
        if (SelectedVendor is null) return;
        var editVm = new VendorEditDialogViewModel(initial: SelectedVendor);
        var ok = await _dialog.ShowDialogAsync(editVm);
        if (ok != true) return;
        await RunAsync(async () =>
        {
            var updated = await _service.UpdateAsync(editVm.ToUpdateDto(SelectedVendor.Id));
            var idx = Vendors.IndexOf(SelectedVendor);
            if (idx >= 0) Vendors[idx] = updated;
            SelectedVendor = updated;
        });
    }

    [RelayCommand(CanExecute = nameof(CanInteractOnSelection))]
    private async Task DeleteAsync()
    {
        if (SelectedVendor is null) return;
        var ok = await _dialog.ConfirmAsync("Xóa nhà cung cấp",
            $"Bạn có chắc muốn xóa \"{SelectedVendor.VendorName}\"? Hành động này có thể khôi phục trong 30 ngày.");
        if (!ok) return;
        var toRemove = SelectedVendor;
        await RunAsync(async () =>
        {
            await _service.DeleteAsync(toRemove.Id);
            Vendors.Remove(toRemove);
        });
    }

    // Centralizes IsBusy management + error-to-dialog conversion so every command
    // body stays focused on its actual logic.
    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            await _dialog.ShowMessageAsync("Lỗi", ex.Message);
        }
        finally { IsBusy = false; }
    }

    private bool CanInteract() => !IsBusy;
    private bool CanInteractOnSelection() => !IsBusy && SelectedVendor is not null;
}
```

Why these refactors:
- `IsBusy` notifies all three command `CanExecute` → buttons grey out during async work, no double-submits.
- `RunAsync` helper deduplicates the IsBusy/try/catch boilerplate across Add/Edit/Delete.
- `LoadAsync` accepts `CancellationToken` so navigating away can cancel mid-load (Plan 04+ can wire this).
- After Edit, re-select the updated record so subsequent operations stay coherent.

> Note: `VendorEditDialogViewModel` is implemented in Task 8. This file references it — Task 8 creates it. To compile this task in isolation, a stub is created in Step 7.4 below.

- [ ] **Step 7.4: Create temporary stub VendorEditDialogViewModel so this task compiles**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Vendors/VendorEditDialogViewModel.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Vendors;

// Stub — full implementation in Task 8.
public partial class VendorEditDialogViewModel : ViewModelBase
{
    public VendorEditDialogViewModel(VendorDto? initial) { }
    public CreateVendorDto ToCreateDto() =>
        new(null, "stub", null, null, null, null, null, null, null);
    public UpdateVendorDto ToUpdateDto(int id) =>
        new(id, null, "stub", null, null, null, null, null, null);
}
```

- [ ] **Step 7.5: Run tests — pass**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~VendorListViewModelTests"`
Expected: All 6 tests pass.

- [ ] **Step 7.6: Commit**

```bash
git add .
git commit -m "feat(desktop): VendorListViewModel with load/add/edit/delete commands + VM tests"
```

---

## Task 8: VendorEditDialogViewModel — TDD (replace stub with real impl)

**Files:**
- Modify (replace stub): `src/ConstructionFinance.Desktop/ViewModels/Vendors/VendorEditDialogViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Vendors/VendorEditDialogViewModelTests.cs`

- [ ] **Step 8.1: Write failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/Vendors/VendorEditDialogViewModelTests.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.ViewModels.Vendors;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Vendors;

public class VendorEditDialogViewModelTests
{
    [Fact]
    public void New_dialog_for_create_has_empty_fields()
    {
        var vm = new VendorEditDialogViewModel(initial: null);

        vm.IsEditMode.ShouldBeFalse();
        vm.DialogTitle.ShouldBe("Thêm nhà cung cấp");
        vm.VendorName.ShouldBeNullOrEmpty();
    }

    [Fact]
    public void Edit_dialog_pre_fills_fields_from_dto()
    {
        var dto = new VendorDto(5, "V1", "Cty ABC", "0123456789", "Hà Nội", "0901", "x@y.vn", "Mr. A", "note");
        var vm = new VendorEditDialogViewModel(initial: dto);

        vm.IsEditMode.ShouldBeTrue();
        vm.DialogTitle.ShouldBe("Sửa nhà cung cấp");
        vm.VendorName.ShouldBe("Cty ABC");
        vm.TaxCode.ShouldBe("0123456789");
        vm.Email.ShouldBe("x@y.vn");
    }

    [Fact]
    public void ToCreateDto_maps_form_fields()
    {
        var vm = new VendorEditDialogViewModel(initial: null)
        {
            VendorCode = "V1",
            VendorName = "Cty",
            TaxCode = "111",
            Email = "e@x.vn"
        };

        var dto = vm.ToCreateDto();

        dto.VendorCode.ShouldBe("V1");
        dto.VendorName.ShouldBe("Cty");
        dto.TaxCode.ShouldBe("111");
        dto.Email.ShouldBe("e@x.vn");
    }

    [Fact]
    public void ToUpdateDto_carries_id_and_form_fields()
    {
        var vm = new VendorEditDialogViewModel(initial: null) { VendorName = "Cty" };

        var dto = vm.ToUpdateDto(42);

        dto.Id.ShouldBe(42);
        dto.VendorName.ShouldBe("Cty");
    }

    [Fact]
    public void HasError_VendorName_true_when_empty()
    {
        var vm = new VendorEditDialogViewModel(initial: null) { VendorName = "" };
        vm.IsValid.ShouldBeFalse();
        vm.VendorNameError.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void IsValid_true_when_VendorName_filled()
    {
        var vm = new VendorEditDialogViewModel(initial: null) { VendorName = "X" };
        vm.IsValid.ShouldBeTrue();
        vm.VendorNameError.ShouldBeNullOrEmpty();
    }

    [Fact]
    public void Invalid_email_blocks_IsValid()
    {
        var vm = new VendorEditDialogViewModel(initial: null) { VendorName = "X", Email = "not-an-email" };
        vm.IsValid.ShouldBeFalse();
        vm.EmailError.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void ToCreateDto_trims_and_normalizes_blank_strings_to_null()
    {
        var vm = new VendorEditDialogViewModel(initial: null)
        {
            VendorCode = "  ",
            VendorName = "  Cty  ",
            TaxCode = ""
        };

        var dto = vm.ToCreateDto();

        dto.VendorCode.ShouldBeNull();
        dto.VendorName.ShouldBe("Cty");
        dto.TaxCode.ShouldBeNull();
    }
}
```

- [ ] **Step 8.2: Run — fails (stub doesn't have these members)**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~VendorEditDialogViewModelTests"`
Expected: Compile/assertion failures.

- [ ] **Step 8.3: Replace the stub with full implementation**

Replace `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Vendors/VendorEditDialogViewModel.cs` with:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Vendors;

public partial class VendorEditDialogViewModel : ViewModelBase, IHasDialogTitle, IValidatable
{
    public bool IsEditMode { get; }
    public string DialogTitle => IsEditMode ? "Sửa nhà cung cấp" : "Thêm nhà cung cấp";

    [ObservableProperty] private string? _vendorCode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VendorNameError), nameof(IsValid))]
    private string _vendorName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmailError), nameof(IsValid))]
    private string? _email;

    [ObservableProperty] private string? _taxCode;
    [ObservableProperty] private string? _address;
    [ObservableProperty] private string? _phone;
    [ObservableProperty] private string? _contactPerson;
    [ObservableProperty] private string? _notes;

    public VendorEditDialogViewModel(VendorDto? initial)
    {
        IsEditMode = initial is not null;
        if (initial is not null)
        {
            VendorCode = initial.VendorCode;
            VendorName = initial.VendorName;
            TaxCode = initial.TaxCode;
            Address = initial.Address;
            Phone = initial.Phone;
            Email = initial.Email;
            ContactPerson = initial.ContactPerson;
            Notes = initial.Notes;
        }
    }

    public string? VendorNameError =>
        string.IsNullOrWhiteSpace(VendorName) ? "Tên nhà cung cấp là bắt buộc." : null;

    public string? EmailError =>
        !string.IsNullOrWhiteSpace(Email) && !Email!.Contains('@')
            ? "Email không hợp lệ."
            : null;

    // IValidatable — DialogService binds primary button's IsEnabled to this.
    public bool IsValid => VendorNameError is null && EmailError is null;

    // Normalize empty strings to null so the DTO -> entity layer doesn't store "" alongside genuine values.
    private static string? Nz(string? s) => string.IsNullOrWhiteSpace(s) ? null : s!.Trim();

    public CreateVendorDto ToCreateDto() =>
        new(Nz(VendorCode), VendorName.Trim(), Nz(TaxCode), Nz(Address), Nz(Phone), Nz(Email), Nz(ContactPerson), Nz(Notes));

    public UpdateVendorDto ToUpdateDto(int id) =>
        new(id, Nz(VendorCode), VendorName.Trim(), Nz(TaxCode), Nz(Address), Nz(Phone), Nz(Email), Nz(ContactPerson), Nz(Notes));
}
```

Why these additions:
- Implements `IValidatable` so `DialogService` auto-disables the Save button when the form is invalid.
- Inline email validation (cheap) gives users feedback before round-tripping through FluentValidation at the service layer.
- `Nz()` (null-or-trim helper) prevents whitespace pollution in the DB and avoids "" vs null mismatches.

- [ ] **Step 8.4: Run tests — pass**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~VendorEditDialogViewModelTests"`
Expected: All 8 tests pass. Also re-run VendorListViewModelTests:
```bash
dotnet test tests/ConstructionFinance.Desktop.Tests/
```
Expected: 14 tests pass (6 list + 8 edit).

- [ ] **Step 8.5: Commit**

```bash
git add .
git commit -m "feat(desktop): VendorEditDialogViewModel with validation + create/update DTO mapping"
```

---

## Task 9: VendorListView + VendorEditDialog (XAML views)

**Files:**
- Create: `src/ConstructionFinance.Desktop/Views/Vendors/VendorListView.axaml`
- Create: `src/ConstructionFinance.Desktop/Views/Vendors/VendorListView.axaml.cs`
- Create: `src/ConstructionFinance.Desktop/Views/Vendors/VendorEditDialog.axaml`
- Create: `src/ConstructionFinance.Desktop/Views/Vendors/VendorEditDialog.axaml.cs`

> Note: The Edit view is named `VendorEditDialog.axaml` (NOT `VendorEditDialogView.axaml`) — `ViewLocator` (Task 3.1) tries candidate B `*DialogViewModel → *Dialog` (trim "ViewModel" suffix) and resolves to `VendorEditDialog` directly. Keep this naming convention for all future dialogs.

- [ ] **Step 9.1: Create VendorListView.axaml**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Vendors/VendorListView.axaml`:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Vendors"
             x:Class="ConstructionFinance.Desktop.Views.Vendors.VendorListView"
             x:DataType="vm:VendorListViewModel">
    <DockPanel>
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="8" Margin="0,0,0,12">
            <Button Content="+ Thêm" Command="{Binding AddCommand}"/>
            <Button Content="Sửa" Command="{Binding EditCommand}"/>
            <Button Content="Xóa" Command="{Binding DeleteCommand}"/>
            <ProgressBar IsIndeterminate="True" IsVisible="{Binding IsBusy}" Width="100" VerticalAlignment="Center"/>
            <TextBlock Text="{Binding ErrorMessage}" Foreground="Red" VerticalAlignment="Center"/>
        </StackPanel>
        <DataGrid ItemsSource="{Binding Vendors}"
                  SelectedItem="{Binding SelectedVendor}"
                  CanUserSortColumns="True"
                  IsReadOnly="True"
                  GridLinesVisibility="Horizontal"
                  HeadersVisibility="Column">
            <DataGrid.Columns>
                <DataGridTextColumn Header="Mã" Binding="{Binding VendorCode}" Width="100"/>
                <DataGridTextColumn Header="Tên nhà cung cấp" Binding="{Binding VendorName}" Width="*"/>
                <DataGridTextColumn Header="MST" Binding="{Binding TaxCode}" Width="120"/>
                <DataGridTextColumn Header="SĐT" Binding="{Binding Phone}" Width="120"/>
                <DataGridTextColumn Header="Email" Binding="{Binding Email}" Width="200"/>
                <DataGridTextColumn Header="Người liên hệ" Binding="{Binding ContactPerson}" Width="160"/>
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</UserControl>
```

- [ ] **Step 9.2: Create VendorListView.axaml.cs**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Vendors/VendorListView.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Vendors;

public partial class VendorListView : UserControl
{
    public VendorListView() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 9.3: Create VendorEditDialog.axaml**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Vendors/VendorEditDialog.axaml`:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Vendors"
             x:Class="ConstructionFinance.Desktop.Views.Vendors.VendorEditDialog"
             x:DataType="vm:VendorEditDialogViewModel"
             MinWidth="480">
    <Grid ColumnDefinitions="140,*" RowDefinitions="Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto" RowSpacing="8" ColumnSpacing="8">
        <TextBlock Grid.Row="0" Grid.Column="0" Text="Mã NCC" VerticalAlignment="Center"/>
        <TextBox Grid.Row="0" Grid.Column="1" Text="{Binding VendorCode}"/>

        <TextBlock Grid.Row="1" Grid.Column="0" Text="Tên nhà cung cấp *" VerticalAlignment="Center"/>
        <StackPanel Grid.Row="1" Grid.Column="1">
            <TextBox Text="{Binding VendorName}"/>
            <TextBlock Text="{Binding VendorNameError}" Foreground="Red" FontSize="11"
                       IsVisible="{Binding VendorNameError, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
        </StackPanel>

        <TextBlock Grid.Row="2" Grid.Column="0" Text="Mã số thuế" VerticalAlignment="Center"/>
        <TextBox Grid.Row="2" Grid.Column="1" Text="{Binding TaxCode}"/>

        <TextBlock Grid.Row="3" Grid.Column="0" Text="Địa chỉ" VerticalAlignment="Center"/>
        <TextBox Grid.Row="3" Grid.Column="1" Text="{Binding Address}"/>

        <TextBlock Grid.Row="4" Grid.Column="0" Text="Điện thoại" VerticalAlignment="Center"/>
        <TextBox Grid.Row="4" Grid.Column="1" Text="{Binding Phone}"/>

        <TextBlock Grid.Row="5" Grid.Column="0" Text="Email" VerticalAlignment="Center"/>
        <TextBox Grid.Row="5" Grid.Column="1" Text="{Binding Email}"/>

        <TextBlock Grid.Row="6" Grid.Column="0" Text="Người liên hệ" VerticalAlignment="Center"/>
        <TextBox Grid.Row="6" Grid.Column="1" Text="{Binding ContactPerson}"/>

        <TextBlock Grid.Row="7" Grid.Column="0" Text="Ghi chú" VerticalAlignment="Top"/>
        <TextBox Grid.Row="7" Grid.Column="1" Text="{Binding Notes}" AcceptsReturn="True" Height="80" TextWrapping="Wrap"/>
    </Grid>
</UserControl>
```

- [ ] **Step 9.4: Create VendorEditDialog.axaml.cs**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Vendors/VendorEditDialog.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Vendors;

public partial class VendorEditDialog : UserControl
{
    public VendorEditDialog() => AvaloniaXamlLoader.Load(this);
}
```

> ViewLocator already supports both `*ViewModel→*View` and `*DialogViewModel→*Dialog` naming patterns from Task 3.1 — no changes needed here. `VendorListViewModel` resolves to `VendorListView` (candidate A) and `VendorEditDialogViewModel` resolves to `VendorEditDialog` (candidate B).

- [ ] **Step 9.5: Build**

Run: `dotnet build src/ConstructionFinance.Desktop/`
Expected: 0 errors.

- [ ] **Step 9.6: Commit**

```bash
git add .
git commit -m "feat(desktop): Vendor list + edit dialog XAML views + ViewLocator dual-pattern resolution"
```

---

## Task 10: Wire Vendor into MainWindow navigation

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/Views/MainWindow.axaml.cs`
- Modify: `src/ConstructionFinance.Desktop/Common/HostBuilder.cs`

- [ ] **Step 10.1: Register VendorListViewModel in HostBuilder**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Common/HostBuilder.cs`. Inside `ConfigureServices`, after the IDialogService registration, add:
```csharp
                services.AddTransient<ViewModels.Vendors.VendorListViewModel>();
```

- [ ] **Step 10.2: Add NavigateToTag method to MainWindowViewModel**

Replace `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs` with:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.ViewModels.Vendors;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;

    public MainWindowViewModel(IServiceProvider services)
    {
        _services = services;
    }

    [ObservableProperty] private string _title = "Quản lý Tài chính Công trình";
    [ObservableProperty] private string _statusText = $"DB: {AppPaths.DatabaseFile} | Sẵn sàng";
    [ObservableProperty] private object? _currentContent;

    public async Task NavigateToAsync(string tag)
    {
        switch (tag)
        {
            case "Vendors":
                var vm = _services.GetRequiredService<VendorListViewModel>();
                CurrentContent = vm;
                await vm.LoadAsync();
                break;
            default:
                CurrentContent = new PlaceholderViewModel { Tag = tag };
                break;
        }
    }
}

public class PlaceholderViewModel : ViewModelBase
{
    public string Tag { get; set; } = "";
}
```

- [ ] **Step 10.3: Wire the NavigationView selection in MainWindow.axaml.cs**

Replace `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/MainWindow.axaml.cs` with:
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ConstructionFinance.Desktop.ViewModels;
using FluentAvalonia.UI.Controls;

namespace ConstructionFinance.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        var nav = this.FindControl<NavigationView>("Nav");
        if (nav is not null)
            nav.SelectionChanged += OnNavSelectionChanged;
    }

    private async void OnNavSelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && e.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            await vm.NavigateToAsync(tag);
        }
    }
}
```

- [ ] **Step 10.4: Build + run the app to verify end-to-end**

Run:
```bash
dotnet build src/ConstructionFinance.Desktop/
rm -f "$HOME/.local/share/ConstructionFinance/app.db"  # clean state to test migration on startup
dotnet run --project src/ConstructionFinance.Desktop/ &
APP_PID=$!
sleep 5
# Verify DB was created and migrations applied
ls -la "$HOME/.local/share/ConstructionFinance/"
test -f "$HOME/.local/share/ConstructionFinance/app.db" && echo "DB OK" || echo "DB MISSING"
kill $APP_PID 2>/dev/null || true
```
Expected:
- App window opens
- Sidebar items visible
- Click "Nhà cung cấp" → empty vendor list appears with `+ Thêm`, `Sửa`, `Xóa` buttons
- Click `+ Thêm` → ContentDialog opens with the edit form
- Enter "Tên nhà cung cấp" + click "Lưu" → vendor appears in list
- DB file exists at `~/.local/share/ConstructionFinance/app.db`

If running headless, just verify the build + DB file. Manual UI test on a desktop machine.

- [ ] **Step 10.5: Run all tests one more time**

Run from project root:
```bash
dotnet test --configuration Release
```
Expected: Domain (37) + Data (38) + Application (39) + Desktop (14) = **128 tests** pass.

- [ ] **Step 10.6: Commit**

```bash
git add .
git commit -m "feat(desktop): wire Vendor menu in MainWindow + end-to-end CRUD working"
```

---

## Final verification

- [ ] **Step F.1: Clean build + test from scratch**

Run:
```bash
cd /home/hieu-nguyen/management/
dotnet clean
dotnet build --configuration Release
dotnet test --configuration Release --verbosity minimal
```
Expected: 0 errors. **128 tests** pass across all 4 test projects: Domain (37) + Data (38) + Application (39) + Desktop (14).

- [ ] **Step F.2: Verify the database file location matches design**

Run:
```bash
test -f "$HOME/.local/share/ConstructionFinance/app.db" && echo "DB at expected path" || echo "DB missing"
ls "$HOME/.local/share/ConstructionFinance/"
```
Expected: `app.db`, `logs/`, `attachments/`, `Backups/` exist.

- [ ] **Step F.3: Manual UI smoke test (on a desktop)**

Run `dotnet run --project src/ConstructionFinance.Desktop/`. Manual checklist:
- [ ] Window opens, sidebar menu visible, "Nhà cung cấp" selectable
- [ ] Add a vendor → appears in list
- [ ] Edit a vendor → changes persist
- [ ] Delete a vendor (confirm) → disappears from list
- [ ] Restart app → vendor still there (persistence works)
- [ ] Log file populated at `~/.local/share/ConstructionFinance/logs/app-<date>.log`

---

## Deliverables

After this plan:

1. ✅ Avalonia 11 + FluentAvalonia + CommunityToolkit.Mvvm + Microsoft.Extensions.Hosting wired into a working desktop app
2. ✅ SQLite database at `%LocalAppData%/ConstructionFinance/app.db` with auto-migration on startup + cost unit seeding, fatal-error fallback dialog if startup fails
3. ✅ Serilog rolling daily logs at `%LocalAppData%/ConstructionFinance/logs/` with `#if DEBUG` log-level switching and structured output template
4. ✅ MainWindow shell with FluentAvalonia NavigationView (sidebar, compact mode 48px) + status bar
5. ✅ MVVM infrastructure:
   - `ViewLocator` (dual-pattern, cached, Window-safe)
   - `ViewModelBase`
   - `IDialogService` + `DialogService` (ContentDialog wrapper, auto-disables Save on invalid forms via `IValidatable`)
   - Reusable `ConfirmDialog` (defaults to Cancel for destructive prompts)
6. ✅ Vendor CRUD: list view (DataGrid) + edit dialog (inline validation: required + email format + trim/normalize) — fully working end-to-end, commands gated on IsBusy
7. ✅ Desktop.Tests project with 14 VM tests using Moq + FakeDialogService
8. ✅ Pattern locked in for Plan 04 to repeat across Decision/Contract/Project

**What's NOT in this plan** (Plan 04+):
- Decision/Contract/Project CRUD UIs (Plan 04 — replicates the Vendor pattern)
- Cost tree + Payments UI (Plan 05)
- Settings UI, Backup/Restore, Inno Setup (Plan 06)
- Dashboard, Reports, Audit log viewer
- Multi-language (only Vietnamese hard-coded)
- Self-contained Windows publish (`dotnet publish -r win-x64 --self-contained`) — verified at Plan 06
