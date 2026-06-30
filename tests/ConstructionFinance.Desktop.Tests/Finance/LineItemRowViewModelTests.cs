using ConstructionFinance.Application.Calc;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class LineItemRowViewModelTests
{
    [Fact]
    public void Editing_an_input_recomputes_derived_cells_and_raises_event()
    {
        var row = new LineItemRowViewModel();
        var fired = 0;
        row.InputChanged += (_, _) => fired++;

        row.PaymentKLHT = 800m;
        row.RecoveredAdvance = 200m;
        row.DisbursedNSNN = 600m;

        row.Disbursed.ShouldBe(600m);          // NSNN + NgoaiHoi
        row.Expected.ShouldBe(600m);           // payment: KLHT − RecoveredAdvance
        row.IsBalanced.ShouldBeTrue();
        row.KindLabel.ShouldBe("Thanh toán KLHT");
        fired.ShouldBe(3);                      // one per input change
    }

    [Fact]
    public void Advance_only_line_is_classified_as_ung_tien()
    {
        var row = new LineItemRowViewModel { Advance = 500m, DisbursedNSNN = 500m };
        row.KindLabel.ShouldBe("Ứng tiền");
        row.Expected.ShouldBe(500m);
        row.IsBalanced.ShouldBeTrue();
    }

    [Fact]
    public void Unbalanced_line_flags_IsBalanced_false()
    {
        var row = new LineItemRowViewModel { PaymentKLHT = 800m, RecoveredAdvance = 200m, DisbursedNSNN = 100m };
        row.IsBalanced.ShouldBeFalse();        // disbursed 100 ≠ expected 600
    }

    [Fact]
    public void SetCell_parses_money_and_date_columns()
    {
        var row = new LineItemRowViewModel();
        row.SetCell(0, "05/03/2026");
        row.SetCell(1, "Gói A");
        row.SetCell(2, "1.500.000");
        row.SetCell(5, "1.500.000");

        row.Date.ShouldBe(new System.DateOnly(2026, 3, 5));
        row.PackageName.ShouldBe("Gói A");
        row.Advance.ShouldBe(1_500_000m);
        row.DisbursedNSNN.ShouldBe(1_500_000m);
    }

    [Fact]
    public void Roundtrips_through_entity()
    {
        var entity = new ProjectLineItem { SortOrder = 4, Date = new System.DateOnly(2026, 2, 1), PackageName = "X", PaymentKLHT = 700m, DisbursedNSNN = 700m };
        var row = new LineItemRowViewModel(entity);
        row.PaymentKLHT.ShouldBe(700m);

        var back = row.ToEntity();
        back.SortOrder.ShouldBe(4);
        back.PaymentKLHT.ShouldBe(700m);
        back.PackageName.ShouldBe("X");

        var input = row.ToInput();
        input.ShouldBeOfType<LineItemInput>();
        input.DisbursedNSNN.ShouldBe(700m);
    }
}
