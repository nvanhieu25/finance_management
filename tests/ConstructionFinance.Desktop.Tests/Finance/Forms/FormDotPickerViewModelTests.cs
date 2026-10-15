using ConstructionFinance.Application.Forms;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance.Forms;

public class FormDotPickerViewModelTests
{
    private static FormDotRow Row(int dotIndex, Fund fund, DateOnly date, decimal advance = 10_000_000m) =>
        new(new FormDotKey(0, 0, 0, dotIndex), "Vị trí 1", "CHI PHÍ XÂY DỰNG", "Gói A",
            "Cty XL A", "123456789", 50_000_000_000m, 0m, date, $"Đợt {dotIndex}", "CT01",
            fund, advance, 0m, 0m, advance);

    private static readonly IReadOnlyList<LedgerRowViewModel> NoSources = Array.Empty<LedgerRowViewModel>();

    [Fact]
    public void Refresh_lists_only_dots_of_the_requested_fund()
    {
        var vm = new FormDotPickerViewModel(Fund.NSNN);

        vm.Refresh(new[]
        {
            Row(0, Fund.NSNN, new DateOnly(2026, 3, 1)),
            Row(1, Fund.NgoaiHoi, new DateOnly(2026, 3, 2))
        }, NoSources);

        vm.Items.Count.ShouldBe(1);
        vm.Items[0].FundText.ShouldBe("Dự phòng NSNN");
    }

    [Fact]
    public void Refresh_with_no_fund_filter_lists_both_funds()
    {
        var vm = new FormDotPickerViewModel(null);

        vm.Refresh(new[]
        {
            Row(0, Fund.NSNN, new DateOnly(2026, 3, 1)),
            Row(1, Fund.NgoaiHoi, new DateOnly(2026, 3, 2))
        }, NoSources);

        vm.Items.Count.ShouldBe(2);
        vm.Items[1].FundText.ShouldBe("Quỹ dự trữ ngoại hối");
    }

    [Fact]
    public void SelectAll_ticks_every_visible_row_and_reports_the_count()
    {
        var vm = new FormDotPickerViewModel(Fund.NSNN);
        vm.Refresh(new[]
        {
            Row(0, Fund.NSNN, new DateOnly(2026, 3, 1)),
            Row(1, Fund.NSNN, new DateOnly(2026, 4, 1))
        }, NoSources);

        vm.SelectAllCommand.Execute(null);

        vm.SelectedCount.ShouldBe(2);
        vm.SelectionSummary.ShouldBe("Đang chọn 2 đợt");
        vm.SelectedKeys.Count.ShouldBe(2);
    }

    [Fact]
    public void ClearSelection_unticks_everything()
    {
        var vm = new FormDotPickerViewModel(Fund.NSNN);
        vm.Refresh(new[] { Row(0, Fund.NSNN, new DateOnly(2026, 3, 1)) }, NoSources);
        vm.SelectAllCommand.Execute(null);

        vm.ClearSelectionCommand.Execute(null);

        vm.SelectedCount.ShouldBe(0);
        vm.Items[0].IsSelected.ShouldBeFalse();
    }

    [Fact]
    public void Month_filter_narrows_the_visible_rows_only()
    {
        var vm = new FormDotPickerViewModel(Fund.NSNN);
        vm.Refresh(new[]
        {
            Row(0, Fund.NSNN, new DateOnly(2026, 3, 1)),
            Row(1, Fund.NSNN, new DateOnly(2026, 4, 1))
        }, NoSources);
        vm.SelectAllCommand.Execute(null);      // chọn cả hai khi chưa lọc

        vm.MonthFilter = "3";

        vm.Items.Count.ShouldBe(1);
        vm.SelectedCount.ShouldBe(2);           // đợt bị lọc ẩn VẪN nằm trong form
        vm.SelectedKeys.Count.ShouldBe(2);
    }

    [Fact]
    public void Year_filter_narrows_the_visible_rows()
    {
        var vm = new FormDotPickerViewModel(Fund.NSNN);
        vm.Refresh(new[]
        {
            Row(0, Fund.NSNN, new DateOnly(2025, 3, 1)),
            Row(1, Fund.NSNN, new DateOnly(2026, 3, 1))
        }, NoSources);

        vm.YearFilter = "2026";

        vm.Items.Count.ShouldBe(1);
        vm.Items[0].DateText.ShouldBe("01/03/2026");
    }

    [Fact]
    public void SelectAll_only_ticks_what_the_filter_shows()
    {
        var vm = new FormDotPickerViewModel(Fund.NSNN);
        vm.Refresh(new[]
        {
            Row(0, Fund.NSNN, new DateOnly(2026, 3, 1)),
            Row(1, Fund.NSNN, new DateOnly(2026, 4, 1))
        }, NoSources);
        vm.MonthFilter = "4";

        vm.SelectAllCommand.Execute(null);

        vm.SelectedCount.ShouldBe(1);
        vm.SelectedKeys.ShouldContain(new FormDotKey(0, 0, 0, 1));
    }

    [Fact]
    public void Selection_survives_a_refresh_that_inserts_a_dot_above_it()
    {
        var vm = new FormDotPickerViewModel(Fund.NSNN);
        var first = new LedgerRowViewModel(RowKind.Entry);
        var second = new LedgerRowViewModel(RowKind.Entry);

        vm.Refresh(new[] { Row(0, Fund.NSNN, new DateOnly(2026, 3, 1)) }, new[] { second });
        vm.Items[0].IsSelected = true;

        // Người dùng chèn một đợt mới lên trên: đợt cũ tụt xuống chỉ số 1.
        vm.Refresh(new[]
        {
            Row(0, Fund.NSNN, new DateOnly(2026, 2, 1)),
            Row(1, Fund.NSNN, new DateOnly(2026, 3, 1))
        }, new[] { first, second });

        vm.SelectedCount.ShouldBe(1);
        vm.SelectedKeys.ShouldContain(new FormDotKey(0, 0, 0, 1));   // khóa đã trượt theo
        vm.Items[1].IsSelected.ShouldBeTrue();
        vm.Items[0].IsSelected.ShouldBeFalse();
    }

    [Fact]
    public void Token_alignment_survives_a_fund_filter_that_skips_a_row()
    {
        var vm = new FormDotPickerViewModel(Fund.NSNN);
        var skipped = new LedgerRowViewModel(RowKind.Entry);
        var first = new LedgerRowViewModel(RowKind.Entry);
        var second = new LedgerRowViewModel(RowKind.Entry);
        var rows = new[]
        {
            Row(0, Fund.NgoaiHoi, new DateOnly(2026, 1, 1)),   // bị lọc bỏ - không thuộc NSNN
            Row(1, Fund.NSNN, new DateOnly(2026, 3, 1)),
            Row(2, Fund.NSNN, new DateOnly(2026, 4, 1))
        };
        var sources = new[] { skipped, first, second };

        vm.Refresh(rows, sources);

        // Dòng hiển thị đầu tiên là rows[1]; token của nó phải là sources[1] (chỉ số
        // CHƯA lọc), không phải sources[0] (chỉ số đã lọc - lỗi lệch một nếu có).
        vm.Items[0].Token.ShouldBeSameAs(first);

        vm.Items[0].IsSelected = true;
        vm.Refresh(rows, sources);

        vm.SelectedCount.ShouldBe(1);
        vm.SelectedKeys.ShouldContain(new FormDotKey(0, 0, 0, 1));
        vm.Items[0].IsSelected.ShouldBeTrue();
    }

    [Fact]
    public void Selection_of_a_deleted_dot_is_dropped()
    {
        var vm = new FormDotPickerViewModel(Fund.NSNN);
        var only = new LedgerRowViewModel(RowKind.Entry);
        vm.Refresh(new[] { Row(0, Fund.NSNN, new DateOnly(2026, 3, 1)) }, new[] { only });
        vm.Items[0].IsSelected = true;

        vm.Refresh(Array.Empty<FormDotRow>(), Array.Empty<LedgerRowViewModel>());

        vm.SelectedCount.ShouldBe(0);
        vm.SelectedKeys.ShouldBeEmpty();
    }

    [Fact]
    public void Ticking_a_row_raises_SelectionChanged()
    {
        var vm = new FormDotPickerViewModel(Fund.NSNN);
        vm.Refresh(new[] { Row(0, Fund.NSNN, new DateOnly(2026, 3, 1)) }, NoSources);
        var raised = 0;
        vm.SelectionChanged += (_, _) => raised++;

        vm.Items[0].IsSelected = true;

        raised.ShouldBe(1);
    }

    [Fact]
    public void Item_formats_money_and_date_for_display()
    {
        var vm = new FormDotPickerViewModel(Fund.NSNN);
        vm.Refresh(new[] { Row(0, Fund.NSNN, new DateOnly(2026, 3, 1), 10_000_000m) }, NoSources);

        var item = vm.Items[0];
        item.DateText.ShouldBe("01/03/2026");
        item.AdvanceText.ShouldBe("10.000.000");
        item.DisbursedText.ShouldBe("10.000.000");
        item.ContentText.ShouldBe("Đợt 0");
        item.PackageName.ShouldBe("Gói A");
    }

    [Fact]
    public void SaveSelection_then_RestoreSelection_returns_to_the_snapshot()
    {
        var vm = new FormDotPickerViewModel(null);
        vm.Refresh(new[]
        {
            Row(0, Fund.NSNN, new DateOnly(2026, 3, 1)),
            Row(1, Fund.NSNN, new DateOnly(2026, 3, 2))
        }, NoSources);
        vm.Items[0].IsSelected = true;
        var snapshot = vm.SaveSelection();

        vm.Items[0].IsSelected = false;
        vm.Items[1].IsSelected = true;
        vm.RestoreSelection(snapshot);

        vm.Items.Select(i => i.IsSelected).ShouldBe(new[] { true, false });
        vm.SelectedCount.ShouldBe(1);
    }

    [Fact]
    public void Picker_is_a_dialog_with_a_title_and_an_OK_button()
    {
        var vm = new FormDotPickerViewModel(null) { Title = "Chọn đợt — X" };

        ((IHasDialogTitle)vm).DialogTitle.ShouldBe("Chọn đợt — X");
        ((IHasPrimaryButtonText)vm).PrimaryButtonText.ShouldBe("OK");
    }

    [Fact]
    public void SelectAll_ClearSelection_and_RestoreSelection_report_the_change_once()
    {
        var vm = new FormDotPickerViewModel(null);
        vm.Refresh(Enumerable.Range(0, 50).Select(i => Row(i, Fund.NSNN, new DateOnly(2026, 3, 1))).ToList(), NoSources);
        var raised = 0;
        vm.SelectionChanged += (_, _) => raised++;

        vm.SelectAllCommand.Execute(null);
        raised.ShouldBe(1);
        vm.SelectedCount.ShouldBe(50);

        var snapshot = vm.SaveSelection();
        vm.ClearSelectionCommand.Execute(null);
        raised.ShouldBe(2);
        vm.SelectedCount.ShouldBe(0);

        vm.RestoreSelection(snapshot);
        raised.ShouldBe(3);
        vm.SelectedCount.ShouldBe(50);
    }
}
