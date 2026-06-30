using ConstructionFinance.Application.Calc;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Calc;

public class LineItemMappingTests
{
    [Fact]
    public void ToCalcInput_copies_all_raw_fields()
    {
        var line = new ProjectLineItem
        {
            Date = new DateOnly(2026, 2, 20),
            PackageName = "TT KLHT",
            Advance = 0m,
            PaymentKLHT = 800m,
            RecoveredAdvance = 200m,
            DisbursedNSNN = 420m,
            DisbursedNgoaiHoi = 180m,
        };

        var input = line.ToCalcInput();

        input.Date.ShouldBe(new DateOnly(2026, 2, 20));
        input.PackageName.ShouldBe("TT KLHT");
        input.PaymentKLHT.ShouldBe(800m);
        input.RecoveredAdvance.ShouldBe(200m);
        input.DisbursedNSNN.ShouldBe(420m);
        input.DisbursedNgoaiHoi.ShouldBe(180m);
        // and the calc layer agrees on the disbursed total
        ProjectCalculator.ComputeLine(input).Disbursed.ShouldBe(600m);
    }
}
