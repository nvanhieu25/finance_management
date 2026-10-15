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

public static partial class HostBuilder
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
                services.AddSingleton<IPdfExporter, PdfExporter>();
                services.AddSingleton<IMigraDocBuilder, MigraDocBuilder>();
                // Adapter máy in. Bản cài đặt nằm ở HostBuilder.Printing.Windows.cs và CHỈ
                // được biên dịch ở đích net8.0-windows; ở đích net8.0 (bộ test) hàm partial
                // này không có thân, lời gọi bị xoá hẳn khi biên dịch, nên không IPrinterPort
                // nào được đăng ký và hai lệnh In ra giấy mờ đi — đúng như CanPrint* kiểm.
                AddPrinterServices(services);
                services.AddTransient<ViewModels.Decisions.DecisionListViewModel>();
                services.AddTransient<ViewModels.Contracts.ContractListViewModel>();
                services.AddTransient<ViewModels.Projects.ProjectListViewModel>();
                services.AddTransient<ViewModels.CostItems.CostTreeViewModel>();
                services.AddTransient<ViewModels.Settings.CompanyInfoViewModel>();
                services.AddTransient<ViewModels.Settings.BackupManagementViewModel>();
                services.AddTransient<ViewModels.Settings.PreferencesViewModel>();
                services.AddTransient<ViewModels.Settings.SettingsViewModel>();
                services.AddTransient<ViewModels.Finance.FinanceWorkbookViewModel>();
                services.AddTransient<ViewModels.Finance.WorkbookSummaryViewModel>();
                services.AddSingleton<IServiceProvider>(sp => sp);
                // ViewModels + IDialogService/INavigationService registered in later tasks.
            })
            .Build();
    }

    /// Đăng ký IPrinterPort/IPrinterCatalog của Windows. KHÔNG có thân ở đích net8.0.
    ///
    /// Trước đây chỗ này là `#if NET8_0_WINDOWS` — một ký hiệu mà .NET SDK KHÔNG định nghĩa
    /// (đích net8.0-windows có WINDOWS, WINDOWS7_0, NET8_0, NET8_0_OR_GREATER... nhưng không
    /// có NET8_0_WINDOWS). Một `#if` luôn sai không sinh cảnh báo nào, nên hai lệnh
    /// AddSingleton KHÔNG BAO GIỜ được biên dịch: bản .exe phát hành vẫn chở theo
    /// ConstructionFinance.Printing.Windows.dll nhưng không ai gọi tới, HasPrinterSupport
    /// luôn false, và cả tính năng In ra giấy mờ vĩnh viễn. Build xanh, 1011 test xanh,
    /// không một dấu hiệu nào. Lỗi IM LẶNG.
    ///
    /// Vì thế bản này KHÔNG đổi sang một ký hiệu khác (`#if WINDOWS` chạy được hôm nay nhưng
    /// lại âm thầm sai nếu ai đó sửa điều kiện của ProjectReference). Thay vào đó: hàm
    /// partial này + file cài đặt KHÔNG có tiền xử lý nào, và file đó nằm CÙNG MỘT ItemGroup
    /// với ProjectReference có điều kiện trong .csproj. Hai thứ chỉ có thể cùng xuất hiện
    /// hoặc cùng mất. Nếu ProjectReference mất mà file còn, file gọi thẳng tên
    /// WindowsPrinterPort nên build HỎNG — thay vì lặng lẽ ship một tính năng chết.
    static partial void AddPrinterServices(IServiceCollection services);
}
