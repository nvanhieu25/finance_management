using ConstructionFinance.Data;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.ViewModels;
using ConstructionFinance.Desktop.ViewModels.Decisions;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Desktop.ViewModels.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests;

/// Ghép app THẬT: HostBuilder (đăng ký DI) + migration trên tệp SQLite tạm + điều hướng tới mọi mục nav.
/// `MainWindowViewModel`, `HostBuilder` và nhánh `switch` điều hướng trước đây không có test nào (spec 7E §7), nên
/// suite xanh im lặng về đúng những tệp mà đợt dọn mã sửa nhiều nhất. Test này bắt: quên đăng ký DI một VM còn sống,
/// hoặc nhánh nav trỏ tới VM đã xoá, hoặc migration hỏng trên DB trống.
public class AppCompositionTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"cf-app-{Guid.NewGuid():N}.db");
    private readonly string? _previousDb = Environment.GetEnvironmentVariable("CONSTRUCTIONFINANCE_DB");

    public AppCompositionTests() => Environment.SetEnvironmentVariable("CONSTRUCTIONFINANCE_DB", _dbPath);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("CONSTRUCTIONFINANCE_DB", _previousDb);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
            if (File.Exists(file)) File.Delete(file);
    }

    private static async Task<IHost> BuildMigratedHostAsync()
    {
        var host = ConstructionFinance.Desktop.Common.HostBuilder.Build();
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        return host;
    }

    [Theory]
    [InlineData("FinanceWorkbook", typeof(FinanceWorkbookViewModel))]
    [InlineData("WorkbookSummary", typeof(WorkbookSummaryViewModel))]
    [InlineData("ThongTri", typeof(FinanceWorkbookViewModel))]       // mục Thông tri mở tab Thông tri của sổ
    [InlineData("Decisions", typeof(DecisionListViewModel))]
    [InlineData("Settings", typeof(SettingsViewModel))]
    public async Task Every_nav_item_resolves_and_loads_on_a_fresh_database(string tag, Type expected)
    {
        using var host = await BuildMigratedHostAsync();
        var main = host.Services.GetRequiredService<MainWindowViewModel>();
        var nav = host.Services.GetRequiredService<INavigationService>();

        await main.NavigateToAsync(tag);

        nav.CurrentContent.ShouldBeOfType(expected);
    }

    [Fact]
    public async Task ThongTri_item_lands_on_the_ThongTri_form_tab_of_the_workbook()
    {
        using var host = await BuildMigratedHostAsync();
        var main = host.Services.GetRequiredService<MainWindowViewModel>();
        var nav = host.Services.GetRequiredService<INavigationService>();

        await main.NavigateToAsync("ThongTri");

        var workbook = nav.CurrentContent.ShouldBeOfType<FinanceWorkbookViewModel>();
        workbook.SelectedViTriPage.ShouldBeNull("DB trống: chưa có dự án nào nên chưa có tab");
    }

    [Fact]
    public async Task Unknown_tag_falls_back_to_the_placeholder_instead_of_throwing()
    {
        using var host = await BuildMigratedHostAsync();
        var main = host.Services.GetRequiredService<MainWindowViewModel>();

        await main.NavigateToAsync("khong-co");

        host.Services.GetRequiredService<INavigationService>().CurrentContent.ShouldBeOfType<PlaceholderViewModel>();
    }
}
