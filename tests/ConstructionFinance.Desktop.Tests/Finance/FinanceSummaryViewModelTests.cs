using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class FinanceSummaryViewModelTests
{
    private static FinanceProjectSummary S(int id, string name, decimal advance, decimal nsnn) =>
        new(id, null, name, null, 1_000m,
            ProjectCalculator.Aggregate(new List<LineItemInput>
            {
                new(new System.DateOnly(2026, 1, 1), null, advance, 0m, 0m, nsnn, 0m),
            }));

    private static FinanceSummaryViewModel Create(out Mock<IFinanceProjectService> svc)
    {
        svc = new Mock<IFinanceProjectService>(MockBehavior.Strict);
        svc.Setup(s => s.GetAllSummariesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FinanceProjectSummary> { S(1, "Cầu A12", 500m, 500m), S(2, "Đường B3", 0m, 300m) });
        var nav = new FakeNavigationService();
        var services = new ServiceCollection().BuildServiceProvider();
        return new FinanceSummaryViewModel(svc.Object, nav, services);
    }

    [Fact]
    public async Task LoadAsync_shows_all_rows_and_grand_total()
    {
        var vm = Create(out _);
        await vm.LoadAsync();

        vm.Rows.Count.ShouldBe(2);
        vm.GrandSumAdvance.ShouldBe(500m);
        vm.GrandSumNSNN.ShouldBe(800m);            // 500 + 300
        vm.GrandTotalDisbursed.ShouldBe(800m);
    }

    [Fact]
    public async Task FilterText_narrows_rows_and_grand_total_instantly()
    {
        var vm = Create(out _);
        await vm.LoadAsync();

        vm.FilterText = "cầu";                      // case-insensitive substring
        vm.Rows.Count.ShouldBe(1);
        vm.Rows[0].Name.ShouldBe("Cầu A12");
        vm.GrandSumNSNN.ShouldBe(500m);            // only the matching project
    }
}
