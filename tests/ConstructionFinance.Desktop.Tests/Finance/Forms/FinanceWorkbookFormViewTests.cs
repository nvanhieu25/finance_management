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
}
