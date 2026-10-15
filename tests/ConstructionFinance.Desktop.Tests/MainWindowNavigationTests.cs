using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;
using ConstructionFinance.Domain.Finance;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests;

public class MainWindowNavigationTests
{
    private static (MainWindowViewModel Main, FakeNavigationService Nav) Create()
    {
        var project = new Project { Id = 7, Name = "Dự án A", ViTris = { new ViTri { Name = "Vị trí 1" } } };
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.GetAllSummariesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FinanceProjectSummary>());
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Project> { new() { Id = 7, Name = "Dự án A" } });
        service.Setup(s => s.GetTreeAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        var companyInfo = new Mock<ICompanyInfoService>();
        companyInfo.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyInfoDto(null, null, null, null, null, null, null, null));

        var services = new ServiceCollection();
        services.AddTransient(_ => new FinanceWorkbookViewModel(
            service.Object,
            new SummaryPageViewModel(new FinanceSummaryViewModel(
                service.Object, Mock.Of<IFinanceImportService>(), Mock.Of<IFinanceExportService>(), Mock.Of<IDialogService>())),
            Mock.Of<IDialogService>(), new ServiceCollection().BuildServiceProvider(),
            Mock.Of<IFormExcelExporter>(), companyInfo.Object));
        var nav = new FakeNavigationService();
        return (new MainWindowViewModel(services.BuildServiceProvider(), nav), nav);
    }

    [Fact]
    public async Task ThongTri_item_opens_the_workbook_on_the_ThongTri_tab()
    {
        var (main, nav) = Create();

        await main.NavigateToAsync("ThongTri");

        var workbook = nav.CurrentContent.ShouldBeOfType<FinanceWorkbookViewModel>();
        workbook.SelectedViTriPage.ShouldBeOfType<FormTabPageViewModel>()
            .Inner.Definition.Id.ShouldBe("thongtri");
    }

    [Fact]
    public async Task ThongTri_item_while_already_on_the_workbook_only_switches_the_tab()
    {
        var (main, nav) = Create();
        await main.NavigateToAsync("FinanceWorkbook");
        var workbook = nav.CurrentContent.ShouldBeOfType<FinanceWorkbookViewModel>();
        await workbook.OpenFormTabAsync("kschi");

        await main.NavigateToAsync("ThongTri");

        nav.CurrentContent.ShouldBeSameAs(workbook);
        workbook.SelectedViTriPage.ShouldBeOfType<FormTabPageViewModel>().Inner.Definition.Id.ShouldBe("thongtri");
    }
}
