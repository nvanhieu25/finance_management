using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.ViewModels.Finance;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class FinanceWorkbookViewModelTests
{
    private static FinanceWorkbookViewModel CreateSut(params FinanceProjectSummary[] summaries)
    {
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.GetAllSummariesAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(summaries);

        var summaryVm = new FinanceSummaryViewModel(
            service.Object,
            Mock.Of<IFinanceImportService>(),
            Mock.Of<IFinanceExportService>(),
            Mock.Of<IDialogService>(),
            Mock.Of<INavigationService>(),
            new ServiceCollection().BuildServiceProvider());

        return new FinanceWorkbookViewModel(
            service.Object,
            new SummaryPageViewModel(summaryVm),
            Mock.Of<IDialogService>(),
            new ServiceCollection().BuildServiceProvider());
    }

    [Fact]
    public async Task LoadAsync_puts_the_pinned_TH_page_first()
    {
        var sut = CreateSut();

        await sut.LoadAsync();

        sut.Pages.ShouldNotBeEmpty();
        sut.Pages[0].ShouldBeOfType<SummaryPageViewModel>();
        sut.Pages[0].IsPinned.ShouldBeTrue();
        sut.Pages[0].Title.ShouldBe("TH");
    }

    [Fact]
    public async Task LoadAsync_selects_the_TH_page()
    {
        var sut = CreateSut();

        await sut.LoadAsync();

        sut.SelectedPage.ShouldBe(sut.Pages[0]);
    }

    [Fact]
    public async Task Header_shows_a_dot_only_when_dirty()
    {
        var sut = CreateSut();
        await sut.LoadAsync();
        var page = sut.Pages[0];
        page.Title = "DA1";

        page.Header.ShouldBe("DA1");

        page.IsDirty = true;
        page.Header.ShouldBe("• DA1");
    }
}
