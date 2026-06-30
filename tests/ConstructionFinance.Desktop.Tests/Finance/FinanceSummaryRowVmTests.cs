using ConstructionFinance.Application.Calc;
using ConstructionFinance.Desktop.ViewModels.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class FinanceSummaryRowVmTests
{
    private static FinanceProjectSummary Summary() =>
        new(
            ProjectId: 7,
            Code: "DA-07",
            Name: "Cầu A12",
            ContractorName: "Nhà thầu A",
            ContractValue: 1_000m,
            Aggregate: ProjectCalculator.Aggregate(new List<LineItemInput>
            {
                new(new System.DateOnly(2025, 5, 1), null, 500m, 0m, 0m, 500m, 0m),
                new(new System.DateOnly(2026, 2, 1), null, 0m, 800m, 200m, 420m, 180m),
            }));

    [Fact]
    public void FromSummary_flattens_identity_and_aggregate()
    {
        var row = FinanceSummaryRowVm.FromSummary(Summary());

        row.ProjectId.ShouldBe(7);
        row.Name.ShouldBe("Cầu A12");
        row.ContractorName.ShouldBe("Nhà thầu A");
        row.ContractValue.ShouldBe(1_000m);
        row.SumAdvance.ShouldBe(500m);
        row.SumRecovered.ShouldBe(200m);
        row.UnrecoveredAdvance.ShouldBe(300m);
        row.SumNSNN.ShouldBe(920m);
        row.SumNgoaiHoi.ShouldBe(180m);
        row.TotalDisbursed.ShouldBe(1_100m);
    }

    [Fact]
    public void FormatYearly_lists_nonzero_parts_per_year_ascending()
    {
        var text = FinanceSummaryRowVm.FromSummary(Summary()).YearlySettlementsText;
        text.ShouldContain("2025:");
        text.ShouldContain("2026:");
        text.IndexOf("2025").ShouldBeLessThan(text.IndexOf("2026")); // ascending
        text.ShouldContain("Ứng 500");   // 2025 advance
        text.ShouldContain("KLHT 800");  // 2026 KLHT
    }
}
