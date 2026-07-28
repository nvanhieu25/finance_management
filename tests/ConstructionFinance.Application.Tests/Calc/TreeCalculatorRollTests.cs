// TreeCalculatorRollTests.cs
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Calc;

public class TreeCalculatorRollTests
{
    [Fact]
    public void Roll_sums_fields_and_merges_yearly_by_fiscal_year()
    {
        var p1 = TreeCalculator.AggregatePackage(new[]
        {
            new DotInput(new DateOnly(2026, 3, 1), Fund.NSNN, 10_000_000m, 0m, 0m),
        });
        var p2 = TreeCalculator.AggregatePackage(new[]
        {
            new DotInput(new DateOnly(2026, 5, 1), Fund.NSNN, 0m, 200_000_000m, 5_000_000m),
            new DotInput(new DateOnly(2027, 4, 1), Fund.NgoaiHoi, 0m, 30_000_000m, 0m), // FY 2027
        });

        var group = TreeCalculator.Roll(new[] { p1, p2 });

        group.DisbursedNSNN.ShouldBe(205_000_000m);
        group.DisbursedNgoaiHoi.ShouldBe(30_000_000m);
        group.UnrecoveredAdvance.ShouldBe(5_000_000m); // 10tr − 5tr
        group.Yearly.Count.ShouldBe(2);                 // FY2026 and FY2027
        group.Yearly.Single(y => y.FiscalYear == 2026).NSNN.ShouldBe(205_000_000m);
        group.Yearly.Single(y => y.FiscalYear == 2027).NgoaiHoi.ShouldBe(30_000_000m);
    }

    [Fact]
    public void Roll_of_empty_is_zero()
    {
        var r = TreeCalculator.Roll(System.Array.Empty<TreeAggregate>());
        r.TotalDisbursed.ShouldBe(0m);
        r.Yearly.ShouldBeEmpty();
    }
}
