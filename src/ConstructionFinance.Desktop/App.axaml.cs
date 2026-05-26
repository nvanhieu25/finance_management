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
            desktop.ShutdownRequested += (_, _) =>
            {
                Host?.Dispose();
                Log.CloseAndFlush();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
