using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class LedgerRowViewModelTests
{
    [Fact]
    public void Entry_row_AdvanceText_parses_money_and_raises_InputChanged()
    {
        var row = new LedgerRowViewModel(RowKind.Entry);
        var fired = 0;
        row.InputChanged += (_, _) => fired++;

        row.AdvanceText = "10.000.000";

        row.Advance.ShouldBe(10_000_000m);
        fired.ShouldBe(1);
    }

    [Fact]
    public void Entry_row_KlhtText_and_RecoveredText_parse_money_and_raise_InputChanged()
    {
        var row = new LedgerRowViewModel(RowKind.Entry);
        var fired = 0;
        row.InputChanged += (_, _) => fired++;

        row.KlhtText = "1.200.000";
        row.RecoveredText = "300.000";

        row.PaymentKLHT.ShouldBe(1_200_000m);
        row.RecoveredAdvance.ShouldBe(300_000m);
        fired.ShouldBe(2);
    }

    [Fact]
    public void FundText_round_trips_with_Fund_enum()
    {
        var row = new LedgerRowViewModel(RowKind.Entry);

        row.Fund = Fund.NgoaiHoi;
        row.FundText.ShouldBe("Ngoại hối");

        row.FundText = "NSNN";
        row.Fund.ShouldBe(Fund.NSNN);

        row.FundText = "Ngoại hối";
        row.Fund.ShouldBe(Fund.NgoaiHoi);
    }

    [Fact]
    public void FundText_change_raises_InputChanged()
    {
        var row = new LedgerRowViewModel(RowKind.Entry);
        var fired = 0;
        row.InputChanged += (_, _) => fired++;

        row.FundText = "Ngoại hối";   // default Fund is NSNN — this is an actual change

        fired.ShouldBe(1);
    }

    [Fact]
    public void DateText_parses_dd_MM_yyyy_into_Date()
    {
        var row = new LedgerRowViewModel(RowKind.Entry);
        var fired = 0;
        row.InputChanged += (_, _) => fired++;

        row.DateText = "05/03/2026";

        row.Date.ShouldBe(new System.DateOnly(2026, 3, 5));
        fired.ShouldBe(1);

        row.DateText = "rác";   // invalid → unchanged
        row.Date.ShouldBe(new System.DateOnly(2026, 3, 5));
    }

    [Fact]
    public void ToDot_builds_Dot_from_typed_values()
    {
        var row = new LedgerRowViewModel(RowKind.Entry)
        {
            SortOrder = 3,
            Date = new System.DateOnly(2026, 1, 15),
            Content = "Thanh toán đợt 1",
            DocumentNo = "01/TT",
            Fund = Fund.NgoaiHoi,
            Advance = 100_000m,
            PaymentKLHT = 200_000m,
            RecoveredAdvance = 50_000m
        };

        var dot = row.ToDot();

        dot.ShouldBeOfType<Dot>();
        dot.SortOrder.ShouldBe(3);
        dot.Date.ShouldBe(new System.DateOnly(2026, 1, 15));
        dot.Content.ShouldBe("Thanh toán đợt 1");
        dot.DocumentNo.ShouldBe("01/TT");
        dot.Fund.ShouldBe(Fund.NgoaiHoi);
        dot.Advance.ShouldBe(100_000m);
        dot.PaymentKLHT.ShouldBe(200_000m);
        dot.RecoveredAdvance.ShouldBe(50_000m);
    }

    [Fact]
    public void Package_row_exposes_name_and_money_facades()
    {
        var row = new LedgerRowViewModel(RowKind.Package)
        {
            Name = "Gói thầu A",
            ContractorName = "Cty X",
            BankAccount = "12345",
            ContractValueText = "1.000.000",
            ContractAnnexText = "50.000"
        };

        row.ContractValue.ShouldBe(1_000_000m);
        row.ContractAnnexValue.ShouldBe(50_000m);
        row.ContractValueText.ShouldBe("1.000.000");
    }

    [Fact]
    public void Package_row_ignores_money_entry_facades()
    {
        var row = new LedgerRowViewModel(RowKind.Package);
        var fired = 0;
        row.InputChanged += (_, _) => fired++;

        // Entry-only facades should not blow up on a Package row, and should not fire InputChanged
        // (Package rows have no Advance/KLHT/RecoveredAdvance of their own).
        row.AdvanceText = "1.000";
        row.KlhtText = "1.000";
        row.RecoveredText = "1.000";

        row.Advance.ShouldBe(0m);
        row.PaymentKLHT.ShouldBe(0m);
        row.RecoveredAdvance.ShouldBe(0m);
        fired.ShouldBe(0);
    }

    [Fact]
    public void Group_row_reuses_Name_property()
    {
        var row = new LedgerRowViewModel(RowKind.Group) { Name = "Nhóm chi phí 1" };
        row.Name.ShouldBe("Nhóm chi phí 1");
    }

    [Fact]
    public void Kind_and_Depth_are_settable_at_construction()
    {
        var row = new LedgerRowViewModel(RowKind.Total) { Depth = 0 };
        row.Kind.ShouldBe(RowKind.Total);
        row.Depth.ShouldBe(0);
    }

    [Fact]
    public void Computed_display_props_are_plain_settable_strings()
    {
        var row = new LedgerRowViewModel(RowKind.Total)
        {
            DisbursedNsnnText = "1.000",
            DisbursedNgoaiHoiText = "2.000",
            UnrecoveredAdvanceText = "3.000",
            TotalDisbursedText = "3.000"
        };

        row.DisbursedNsnnText.ShouldBe("1.000");
        row.DisbursedNgoaiHoiText.ShouldBe("2.000");
        row.UnrecoveredAdvanceText.ShouldBe("3.000");
        row.TotalDisbursedText.ShouldBe("3.000");
    }
}
