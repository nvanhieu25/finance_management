using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ConstructionFinance.Data;
using ConstructionFinance.Desktop.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ConstructionFinance.Desktop;

public partial class App : Avalonia.Application
{
    public static IHost? Host { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override async void OnFrameworkInitializationCompleted()
    {
        Host = Common.HostBuilder.Build();

        // Apply EF Core migrations to the user's SQLite database.
        using (var scope = Host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            // Seed default unit types
            ConstructionFinance.Data.Seed.CostUnitTypeSeeder.Seed(db);
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = Host.Services.GetRequiredService<ViewModels.MainWindowViewModel>();
            desktop.MainWindow = new Views.MainWindow { DataContext = vm };
            _ = vm.InitializeAsync();
            desktop.ShutdownRequested += async (_, _) =>
            {
                if (Host is not null)
                {
                    try
                    {
                        using var scope = Host.Services.CreateScope();
                        var settings = scope.ServiceProvider.GetRequiredService<Application.Services.Interfaces.IAppSettingService>();
                        var prefRaw = await settings.GetAsync("auto_backup_on_exit");
                        var enabled = prefRaw is null || string.Equals(prefRaw, "true", StringComparison.OrdinalIgnoreCase);
                        if (!enabled) return;

                        var modifiedRaw = await settings.GetAsync("session_modified");
                        if (!string.Equals(modifiedRaw, "true", StringComparison.OrdinalIgnoreCase)) return;

                        var backup = scope.ServiceProvider.GetRequiredService<Infrastructure.Backup.IBackupService>();
                        var manifest = new Infrastructure.Backup.BackupManifest(
                            FormatVersion: "1.0",
                            AppVersion: "1.0.0",
                            DbSchemaVersion: "20260519_001",
                            CreatedAtUtc: DateTime.UtcNow,
                            CreatedBy: new Infrastructure.Backup.BackupCreator(Environment.MachineName, Environment.UserName, Environment.OSVersion.ToString()),
                            DataSummary: new Infrastructure.Backup.BackupDataSummary(0, 0, 0, 0, 0, 0, 0, 0),
                            DbSizeBytes: File.Exists(AppPaths.DatabaseFile) ? new FileInfo(AppPaths.DatabaseFile).Length : 0,
                            BackupType: "on_exit",
                            Description: "Auto backup on session close",
                            CompanyName: null);
                        await backup.CreateBackupAsync(AppPaths.DatabaseFile, AppPaths.AttachmentsFolder, AppPaths.BackupsFolder, manifest);
                        await settings.SetAsync("session_modified", "false");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Auto-backup on exit failed");
                    }
                }
                Host?.Dispose();
                Log.CloseAndFlush();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
