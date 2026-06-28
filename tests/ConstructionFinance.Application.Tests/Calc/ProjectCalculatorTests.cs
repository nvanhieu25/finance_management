using ConstructionFinance.Application.Calc;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Calc;

public class ProjectCalculatorTests
{
    private static LineItemInput Advance(decimal amount, decimal nsnn, decimal ngoaiHoi) =>
        new(new DateOnly(2026, 1, 5), "Ứng", amount, 0m, 0m, nsnn, ngoaiHoi);

    private static LineItemInput Payment(decimal klht, decimal recovered, decimal nsnn, decimal ngoaiHoi) =>
        new(new DateOnly(2026, 2, 20), "TT KLHT", 0m, klht, recovered, nsnn, ngoaiHoi);

    [Fact]
    public void ComputeLine_advance_disbursed_is_nsnn_plus_ngoaihoi_expected_is_advance()
    {
        var r = ProjectCalculator.ComputeLine(Advance(amount: 500m, nsnn: 500m, ngoaiHoi: 0m));
        r.Kind.ShouldBe(LineItemKind.Advance);
        r.Disbursed.ShouldBe(500m);
        r.Expected.ShouldBe(500m);
        r.IsBalanced.ShouldBeTrue();
    }

    [Fact]
    public void ComputeLine_payment_expected_is_klht_minus_recovered()
    {
        // KLHT 800 − thu hồi 200 = 600; split NSNN 420 + Ngoại hối 180 = 600 → balanced.
        var r = ProjectCalculator.ComputeLine(Payment(klht: 800m, recovered: 200m, nsnn: 420m, ngoaiHoi: 180m));
        r.Kind.ShouldBe(LineItemKind.Payment);
        r.Disbursed.ShouldBe(600m);
        r.Expected.ShouldBe(600m);
        r.IsBalanced.ShouldBeTrue();
    }

    [Fact]
    public void ComputeLine_flags_imbalance_when_split_does_not_match_klht_minus_recovered()
    {
        // KLHT 300 − thu hồi 100 = 200 expected, but split sums to 150 → not balanced.
        var r = ProjectCalculator.ComputeLine(Payment(klht: 300m, recovered: 100m, nsnn: 150m, ngoaiHoi: 0m));
        r.Expected.ShouldBe(200m);
        r.Disbursed.ShouldBe(150m);
        r.IsBalanced.ShouldBeFalse();
    }

    [Fact]
    public void Aggregate_sums_columns_and_derives_unrecovered_and_total()
    {
        var items = new[]
        {
            Advance(amount: 500m, nsnn: 500m, ngoaiHoi: 0m),         // ứng 500
            Payment(klht: 800m, recovered: 200m, nsnn: 420m, ngoaiHoi: 180m), // giải ngân 600
        };

        var a = ProjectCalculator.Aggregate(items);

        a.SumAdvance.ShouldBe(500m);
        a.SumRecovered.ShouldBe(200m);
        a.SumKLHT.ShouldBe(800m);
        a.SumNSNN.ShouldBe(920m);            // 500 + 420
        a.SumNgoaiHoi.ShouldBe(180m);        // 0 + 180
        a.UnrecoveredAdvance.ShouldBe(300m); // 500 − 200
        a.TotalDisbursed.ShouldBe(1100m);    // 920 + 180
    }

    [Fact]
    public void Aggregate_empty_returns_zeros()
    {
        var a = ProjectCalculator.Aggregate(System.Array.Empty<LineItemInput>());
        a.SumAdvance.ShouldBe(0m);
        a.TotalDisbursed.ShouldBe(0m);
        a.YearlySettlements.ShouldBeEmpty();
    }
}
