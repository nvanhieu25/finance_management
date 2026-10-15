using Avalonia.Controls;
using Avalonia.Threading;
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Desktop.Views.Finance;
using ConstructionFinance.Domain.Finance;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Avalonia.VisualTree;
using ConstructionFinance.Desktop.Views.Finance.Forms;
using Shouldly;
using Xunit;
namespace ConstructionFinance.Desktop.Tests.Finance.Forms;
/// Mở tab biểu mẫu trong FinanceWorkbookView THẬT: dựng, vẽ, và KHÔNG tự đánh dấu dơ (TextBox báo "" lúc dựng
/// từng làm tab dơ ngay khi mở).
public class FinanceWorkbookFormViewTests
{
    private static async Task<(FinanceWorkbookViewModel Vm, Mock<IFinanceProjectService> Service)> LoadWorkbookAsync()
    {
        var project = new Project { Id = 7, Name = "Dự án A", ViTris = { new ViTri { Name = "Vị trí 1" } } };
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Project> { new() { Id = 7, Name = "Dự án A" } });
        service.Setup(s => s.GetTreeAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        var companyInfo = new Mock<ICompanyInfoService>();
        companyInfo.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new CompanyInfoDto(null, null, null, null, null, null, null, null));
        var vm = new FinanceWorkbookViewModel(service.Object, Mock.Of<IDialogService>(), Mock.Of<IFormExcelExporter>(), companyInfo.Object);
        await vm.LoadAsync();
        await vm.OpenFormTabAsync("thongtri");
        return (vm, service);
    }

    [Fact]
    public async Task Opening_a_form_tab_in_the_real_view_draws_it_and_leaves_the_workbook_clean()
    {
        var project = new Project { Id = 7, Name = "Dự án A", ViTris = { new ViTri { Name = "Vị trí 1", CostGroups = { new CostGroup { Name = "CHI PHÍ XÂY DỰNG", Packages = { new Package { Name = "Gói A", ContractorName = "Cty A", Dots = {
            new Dot { Date = new DateOnly(2026,3,1), Fund = Fund.NSNN, Advance = 10_000_000m, Content = "Tạm ứng lần 1" } } } } } } } } };
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Project> { new() { Id = 7, Name = "Dự án A" } });
        service.Setup(s => s.GetTreeAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        var companyInfo = new Mock<ICompanyInfoService>();
        companyInfo.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new CompanyInfoDto(null,null,null,null,null,null,null,null,"Sở Tài chính","Ban QLDA"));
        var vm = new FinanceWorkbookViewModel(service.Object, Mock.Of<IDialogService>(), Mock.Of<IFormExcelExporter>(), companyInfo.Object);
        await vm.LoadAsync();
        await vm.OpenFormTabAsync("thongtri");
        HeadlessTestSession.Run(() =>
        {
            var win = new Window { Content = new FinanceWorkbookView { DataContext = vm }, Width = 1300, Height = 1250 };
            win.Show(); Dispatcher.UIThread.RunJobs();

            vm.IsDirty.ShouldBeFalse();
            vm.ViTriPages.FormTab("thongtri").IsDirty.ShouldBeFalse();
            win.GetVisualDescendants().OfType<FormSheetControl>().Count().ShouldBe(1);
            win.Close();
        });
    }

    /// Chuyển tab KHÔNG được dựng lại view: sổ Vị trí nhiều dòng tốn hàng giây mỗi lần dựng. View dựng một lần, lần sau
    /// chỉ ẩn/hiện; chỉ view của tab đã chọn mới tồn tại; tab bị xoá thì view đi theo.
    [Fact]
    public async Task Switching_tabs_reuses_each_tabs_view_and_drops_views_of_removed_tabs()
    {
        var (vm, _) = await LoadWorkbookAsync();
        HeadlessTestSession.Run(() =>
        {
            var view = new FinanceWorkbookView { DataContext = vm };
            var win = new Window { Content = view, Width = 1300, Height = 900 };
            win.Show(); Dispatcher.UIThread.RunJobs();
            var host = view.FindControl<PagesHost>("PageHost")!;

            var ledger = vm.ViTriPages.OfType<ViTriPageViewModel>().First();
            vm.SelectedViTriPage = ledger; Dispatcher.UIThread.RunJobs();
            var ledgerView = host.Children.Single(c => c.IsVisible);
            vm.SelectedViTriPage = vm.ViTriPages.FormTab("kschi"); Dispatcher.UIThread.RunJobs();
            vm.SelectedViTriPage = ledger; Dispatcher.UIThread.RunJobs();

            host.Children.Single(c => c.IsVisible).ShouldBeSameAs(ledgerView);   // cùng đối tượng view, không dựng lại
            host.Children.Count.ShouldBeLessThan(vm.ViTriPages.Count);          // chỉ dựng tab đã từng được chọn, không dựng cả 6 tab
            host.Children.Count(c => c.IsVisible).ShouldBe(1);

            vm.AddProjectCommand.Execute(null); Dispatcher.UIThread.RunJobs();   // đổi sang dự án mới: mọi tab cũ biến mất
            host.Children.ShouldNotContain(ledgerView);
            win.Close();
        });
    }
}
