using ConstructionFinance.Application;
using ConstructionFinance.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using ConstructionFinance.Desktop.ViewModels;

namespace ConstructionFinance.Desktop.Common;

public static class HostBuilder
{
    public static IHost Build()
    {
        AppPaths.EnsureDirectoriesExist();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File(
                path: Path.Combine(AppPaths.LogsFolder, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();

        return Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((_, services) =>
            {
                services.AddDbContext<AppDbContext>(opt =>
                    opt.UseSqlite(AppPaths.ConnectionString));
                services.AddDataServices();
                services.AddApplicationServices();
                services.AddSingleton<ViewModels.MainWindowViewModel>();
                services.AddSingleton<Services.IDialogService, Services.DialogService>();
                services.AddSingleton<Services.INavigationService, Services.NavigationService>();
                services.AddTransient<ViewModels.Vendors.VendorListViewModel>();
                services.AddTransient<ViewModels.Decisions.DecisionListViewModel>();
                services.AddTransient<ViewModels.Contracts.ContractListViewModel>();
                services.AddTransient<ViewModels.Projects.ProjectListViewModel>();
                services.AddTransient<ViewModels.CostItems.CostTreeViewModel>();
                services.AddSingleton<IServiceProvider>(sp => sp);
                // ViewModels + IDialogService/INavigationService registered in later tasks.
            })
            .Build();
    }
}
