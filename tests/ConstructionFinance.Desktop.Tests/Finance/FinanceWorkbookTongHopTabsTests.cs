using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Import;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Domain.Finance;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class FinanceWorkbookTongHopTabsTests
{
    private static FinanceWorkbookViewModel CreateSut(int projectId, string name, params ViTri[] viTris)
    {
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.GetAllSummariesAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<FinanceProjectSummary>());
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<Project> { new() { Id = projectId, Name = name } });
        service.Setup(s => s.GetTreeAsync(projectId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Project { Id = projectId, Name = name, ViTris = viTris.ToList() });

        var summaryVm = new FinanceSummaryViewModel(
            service.Object, Mock.Of<IFinanceImportService>(),
            Mock.Of<IFinanceExportService>(), Mock.Of<IDialogService>());

        return new FinanceWorkbookViewModel(
            service.Object, new SummaryPageViewModel(summaryVm),
            Mock.Of<IDialogService>(), new ServiceCollection().BuildServiceProvider());
    }

    private static ViTri ViTriWithAdvance(string name, decimal advance) => new()
    {
        Name = name, SortOrder = 0,
        CostGroups =
        {
            new CostGroup { Name = "G", SortOrder = 0, Packages =
            {
                new Package { Name = "P", SortOrder = 0, Dots =
                {
                    new Dot { Date = new DateOnly(2026, 3, 1), Fund = Fund.NSNN, Advance = advance, SortOrder = 0 }
                } }
            } }
        }
    };

    [Fact]
    public async Task Selecting_a_project_frames_the_strip_with_TongHop_and_QuyetToanNam()
    {
        var sut = CreateSut(1, "DA1", ViTriWithAdvance("V1", 10_000_000m));
        await sut.LoadAsync();

        sut.ViTriPages[0].ShouldBeOfType<ProjectTongHopPageViewModel>();
        sut.ViTriPages[^1].ShouldBeOfType<YearSettlementPageViewModel>();
    }

    [Fact]
    public async Task The_TongHop_tab_reflects_the_ledger_numbers()
    {
        var sut = CreateSut(1, "DA1", ViTriWithAdvance("V1", 10_000_000m));
        await sut.LoadAsync();

        var tongHop = sut.ViTriPages.OfType<ProjectTongHopPageViewModel>().Single();
        tongHop.Inner.Rows[^1].NSNN.ShouldBe("10.000.000");   // TỔNG CỘNG row
    }

    [Fact]
    public async Task The_QuyetToanNam_tab_buckets_by_fiscal_year()
    {
        var sut = CreateSut(1, "DA1", ViTriWithAdvance("V1", 10_000_000m));
        await sut.LoadAsync();

        var year = sut.ViTriPages.OfType<YearSettlementPageViewModel>().Single();
        year.Inner.Rows[0].YearLabel.ShouldBe("Quyết toán năm 2026");
        year.Inner.Rows[0].NSNN.ShouldBe("10.000.000");
    }
}
