// TreeCalculatorPackageTests.cs
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Calc;

public class TreeCalculatorPackageTests
{
    private static DotInput Advance(decimal amt, Fund f, DateOnly d) => new(d, f, amt, 0m, 0m);
    private static DotInput Payment(decimal klht, decimal recovered, Fund f, DateOnly d) => new(d, f, 0m, klht, recovered);

    [Fact]
    public void AggregatePackage_routes_disbursement_by_fund_and_derives_so_du_tam_ung()
    {
        var dots = new[]
        {
            Advance(10_000_000m, Fund.NSNN, new DateOnly(2026, 3, 1)),                 // GN NSNN 10tr
            Payment(200_000_000m, 5_000_000m, Fund.NSNN, new DateOnly(2026, 6, 1)),    // GN NSNN 195tr
            Advance(10_000_000m, Fund.NgoaiHoi, new DateOnly(2026, 7, 1)),             // GN Ngoại hối 10tr
        };

        var a = TreeCalculator.AggregatePackage(dots);

        a.SumAdvance.ShouldBe(20_000_000m);
        a.SumKLHT.ShouldBe(200_000_000m);
        a.SumRecovered.ShouldBe(5_000_000m);
        a.DisbursedNSNN.ShouldBe(205_000_000m);      // 10tr + (200tr − 5tr)
        a.DisbursedNgoaiHoi.ShouldBe(10_000_000m);
        a.TotalDisbursed.ShouldBe(215_000_000m);
        a.UnrecoveredAdvance.ShouldBe(15_000_000m);  // ΣAdvance 20tr − ΣRecovered 5tr = số dư tạm ứng
    }

    [Fact]
    public void AggregatePackage_empty_is_all_zero()
    {
        var a = TreeCalculator.AggregatePackage(System.Array.Empty<DotInput>());
        a.TotalDisbursed.ShouldBe(0m);
        a.UnrecoveredAdvance.ShouldBe(0m);
        a.Yearly.ShouldBeEmpty();
    }
}
