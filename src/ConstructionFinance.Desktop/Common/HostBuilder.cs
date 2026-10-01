using ConstructionFinance.Application;
using ConstructionFinance.Application.Printing;
using ConstructionFinance.Data;
using ConstructionFinance.Infrastructure;
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
                services.AddAppDbContext(AppPaths.ConnectionString);
                services.AddDataServices();
                services.AddApplicationServices();
                services.AddInfrastructureServices();
                services.AddSingleton<ViewModels.MainWindowViewModel>();
                services.AddSingleton<Services.IDialogService, Services.DialogService>();
                services.AddSingleton<Services.INavigationService, Services.NavigationService>();
                services.AddSingleton<Services.IFileLauncher, Services.FileLauncher>();
                services.AddSingleton<IPrintWorkbookWriter, PrintWorkbookWriter>();
                services.AddTransient<ViewModels.Vendors.VendorListViewModel>();
                services.AddTransient<ViewModels.Decisions.DecisionListViewModel>();
                services.AddTransient<ViewModels.Contracts.ContractListViewModel>();
                services.AddTransient<ViewModels.Projects.ProjectListViewModel>();
                services.AddTransient<ViewModels.CostItems.CostTreeViewModel>();
                services.AddTransient<ViewModels.Settings.CompanyInfoViewModel>();
                services.AddTransient<ViewModels.Settings.BackupManagementViewModel>();
                services.AddTransient<ViewModels.Settings.PreferencesViewModel>();
                services.AddTransient<ViewModels.Settings.SettingsViewModel>();
                services.AddTransient<ViewModels.TongHop.TongHopViewModel>();
                services.AddTransient<ViewModels.TongHop.ImportWizardViewModel>();
                services.AddTransient<ViewModels.ThongTri.ThongTriViewModel>();
                services.AddTransient<ViewModels.Finance.FinanceProjectGridViewModel>();
                services.AddTransient<ViewModels.Finance.FinanceSummaryViewModel>();
                services.AddTransient<ViewModels.Finance.SummaryPageViewModel>();
                services.AddTransient<ViewModels.Finance.FinanceWorkbookViewModel>();
                services.AddTransient<ViewModels.Finance.WorkbookSummaryViewModel>();
                services.AddSingleton<IServiceProvider>(sp => sp);
                // ViewModels + IDialogService/INavigationService registered in later tasks.
            })
            .Build();
    }
}
