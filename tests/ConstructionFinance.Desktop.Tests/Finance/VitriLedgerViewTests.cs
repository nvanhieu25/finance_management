using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Desktop.Views.Finance;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

/// Headless-Avalonia tests that drive the REAL VitriLedgerView bound to a real
/// VitriLedgerViewModel — the wiring between View and view-model is exactly where the 7B
/// review's defects lived (SelectedRow never set from a click/focus, money cells losing
/// their "." grouping on edit, a leaked KeyDown handler), and none of that is reachable from
/// the existing view-model-only test suite. See HeadlessTestSupport.cs for how the headless
/// session is bootstrapped (no Avalonia.Headless.XUnit — driven by hand).
public class VitriLedgerViewTests
{
    // ===== Fixtures =====

    /// Two nhóm, two gói, one đợt — deliberately more than the "sole group/sole package"
    /// case the toolbar's Add buttons fall back to, so a test built on this fixture only
    /// passes if a row can actually be selected directly (the old code had no way to do
    /// that once there was more than one nhóm/gói).
    private static ViTri BuildTwoGroupsTwoPackagesOneEntryFixture()
    {
        var viTri = new ViTri { Name = "Vị trí test" };

        var group1 = new CostGroup { Name = "Nhóm 1", SortOrder = 0 };
        var package1 = new Package { Name = "Gói A", SortOrder = 0 };
        package1.Dots.Add(new Dot { Content = "Đợt 1", SortOrder = 0, Fund = Fund.NSNN });
        group1.Packages.Add(package1);

        var group2 = new CostGroup { Name = "Nhóm 2", SortOrder = 1 };
        var package2 = new Package { Name = "Gói B", SortOrder = 0 };
        group2.Packages.Add(package2);

        viTri.CostGroups.Add(group1);
        viTri.CostGroups.Add(group2);
        return viTri;
        // Rows end up: [0] Total, [1] Nhóm 1, [2] Gói A, [3] Đợt 1, [4] Nhóm 2, [5] Gói B
    }

    /// One nhóm/gói/đợt — enough to reach every money cell in every row kind. Every money
    /// field starts at 0m (MoneyText.Format(0m) is "", so every money TextBox starts empty —
    /// keystrokes then land cleanly instead of appending onto a pre-existing value).
    private static ViTri BuildOneOfEachRowKindFixture()
    {
        var viTri = new ViTri { Name = "Vị trí test" };
        var group = new CostGroup { Name = "Nhóm 1", SortOrder = 0 };
        var package = new Package { Name = "Gói A", SortOrder = 0 };
        package.Dots.Add(new Dot { Content = "Đợt 1", SortOrder = 0, Fund = Fund.NSNN });
        group.Packages.Add(package);
        viTri.CostGroups.Add(group);
        return viTri;
        // Rows: [0] Total, [1] Nhóm 1, [2] Gói A, [3] Đợt 1
    }

    // ===== Headless plumbing =====

    private sealed record Ledger(Window Window, VitriLedgerView View, VitriLedgerViewModel ViewModel);

    /// Builds a real VitriLedgerView bound to a real VitriLedgerViewModel, hosts it in a
    /// (headless) Window, shows it, and pumps the dispatcher so the ItemsControl realizes a
    /// container for every row (ItemsControl's default panel isn't virtualizing, so — unlike
    /// a DataGrid/ListBox — every row gets a container regardless of what's scrolled into
    /// view).
    private static Ledger CreateLedger(ViTri viTri)
    {
        var vm = new VitriLedgerViewModel(viTri);
        var view = new VitriLedgerView { DataContext = vm };
        var window = new Window { Content = view, Width = 1900, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new Ledger(window, view, vm);
    }

    private static Control FindRowContainer(VitriLedgerView view, int rowIndex)
    {
        var rowsHost = view.FindControl<ItemsControl>("RowsHost")!;
        var container = rowsHost.ContainerFromIndex(rowIndex) as Control;
        return container ?? throw new InvalidOperationException($"No realized container for row {rowIndex}.");
    }

    private static Grid FindRowGrid(Control rowContainer) =>
        rowContainer.GetVisualDescendants().OfType<Grid>().First(g => g.Classes.Contains("gridRow"));

    /// Mirrors VitriLedgerView's own MoveFocus/TryFocusEditable: finds the DIRECT child of
    /// the row's Grid at the given column (skipping the full-width ColumnSpan=14 overlay
    /// borders every row template appends at the end).
    private static Control FindCellControl(VitriLedgerView view, int rowIndex, int column)
    {
        var rowGrid = FindRowGrid(FindRowContainer(view, rowIndex));
        return rowGrid.Children.OfType<Control>()
            .First(c => Grid.GetColumn(c) == column && Grid.GetColumnSpan(c) <= 1);
    }

    private static TextBox FindCellTextBox(VitriLedgerView view, int rowIndex, int column)
    {
        var cell = FindCellControl(view, rowIndex, column);
        if (cell is TextBox direct) return direct;
        return cell.GetVisualDescendants().OfType<TextBox>().FirstOrDefault()
            ?? throw new InvalidOperationException($"No TextBox at row {rowIndex}, column {column}.");
    }

    /// Simulates a real mouse click (press + release) at the given control's own center,
    /// translated into the window's coordinate space — used for the "clicking a
    /// non-focusable part of a row" case, where nothing can be focused instead.
    private static void Click(Window window, Control control)
    {
        var center = new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        var point = control.TranslatePoint(center, window)
            ?? throw new InvalidOperationException("Control is not in the window's visual tree.");
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    // ===== FIX 1: focusing/clicking a row selects it, and delete then actually removes it =====

    [Fact]
    public void Focusing_a_cell_in_a_Package_row_selects_it_and_DeleteSelected_removes_it()
    {
        HeadlessTestSession.Run(() =>
        {
            var (window, view, vm) = CreateLedger(BuildTwoGroupsTwoPackagesOneEntryFixture());
            var packageRow = vm.Rows.Single(r => r.Kind == RowKind.Package && r.Name == "Gói B");

            var nameBox = FindCellTextBox(view, vm.Rows.IndexOf(packageRow), LedgerColumnWidths.NoiDungIndex);
            nameBox.Focus();
            Dispatcher.UIThread.RunJobs();

            vm.SelectedRow.ShouldBeSameAs(packageRow);

            vm.DeleteSelectedCommand.Execute(null);

            vm.Rows.ShouldNotContain(r => r.Name == "Gói B");
            window.Close();
        });
    }

    [Fact]
    public void Focusing_a_cell_in_a_Group_row_selects_it_and_DeleteSelected_removes_it_and_its_package()
    {
        HeadlessTestSession.Run(() =>
        {
            var (window, view, vm) = CreateLedger(BuildTwoGroupsTwoPackagesOneEntryFixture());
            var groupRow = vm.Rows.Single(r => r.Kind == RowKind.Group && r.Name == "Nhóm 2");

            var nameBox = FindCellTextBox(view, vm.Rows.IndexOf(groupRow), LedgerColumnWidths.NoiDungIndex);
            nameBox.Focus();
            Dispatcher.UIThread.RunJobs();

            vm.SelectedRow.ShouldBeSameAs(groupRow);

            vm.DeleteSelectedCommand.Execute(null);

            vm.Rows.ShouldNotContain(r => r.Name == "Nhóm 2" || r.Name == "Gói B");
            window.Close();
        });
    }

    [Fact]
    public void Focusing_a_cell_in_an_Entry_row_selects_it_and_DeleteSelected_removes_only_that_entry()
    {
        HeadlessTestSession.Run(() =>
        {
            var (window, view, vm) = CreateLedger(BuildTwoGroupsTwoPackagesOneEntryFixture());
            var entryRow = vm.Rows.Single(r => r.Kind == RowKind.Entry);

            // Entry's Nội dung cell (column 1) is an overflowCell TextBox, same column used
            // for Group/Package names — the same LocateCell path handles all three kinds.
            var contentBox = FindCellTextBox(view, vm.Rows.IndexOf(entryRow), LedgerColumnWidths.NoiDungIndex);
            contentBox.Focus();
            Dispatcher.UIThread.RunJobs();

            vm.SelectedRow.ShouldBeSameAs(entryRow);

            vm.DeleteSelectedCommand.Execute(null);

            vm.Rows.ShouldNotContain(r => r.Kind == RowKind.Entry);
            vm.Rows.ShouldContain(r => r.Name == "Gói A"); // the entry's own package survives
            window.Close();
        });
    }

    [Fact]
    public void Clicking_a_non_focusable_computed_cell_in_a_row_still_selects_it()
    {
        HeadlessTestSession.Run(() =>
        {
            var (window, view, vm) = CreateLedger(BuildTwoGroupsTwoPackagesOneEntryFixture());
            var groupRow = vm.Rows.Single(r => r.Kind == RowKind.Group && r.Name == "Nhóm 2");

            // The "GN NSNN" cell is a read-only, non-focusable TextBlock (computed subtotal)
            // wrapped in a Border — nothing here can ever take keyboard focus, so this only
            // proves out the PointerPressed path, not GotFocus.
            var cell = FindCellControl(view, vm.Rows.IndexOf(groupRow), LedgerColumnWidths.GnNsnnIndex);
            cell.Focusable.ShouldBeFalse();

            Click(window, cell);

            vm.SelectedRow.ShouldBeSameAs(groupRow);
            window.Close();
        });
    }

    // Group rows carry Background="{DynamicResource AccentSoftBrush}" already (their own
    // styling, unrelated to selection), so they were ALREADY hit-testable through an empty
    // gridCellBorder cell before this fix — the test above alone would keep passing even if
    // the Background="Transparent" added to the Package/Entry row Grids were reverted. These
    // two pin exactly that: Package/Entry rows had NO Background of their own, so without the
    // fix a click there falls through the row entirely (verified failing — see the report).

    [Fact]
    public void Clicking_a_non_focusable_computed_cell_in_a_Package_row_still_selects_it()
    {
        HeadlessTestSession.Run(() =>
        {
            var (window, view, vm) = CreateLedger(BuildTwoGroupsTwoPackagesOneEntryFixture());
            var packageRow = vm.Rows.Single(r => r.Kind == RowKind.Package && r.Name == "Gói A");

            // "Số dư TU" — a read-only computed TextBlock wrapped in a Border, same shape as
            // the Group case above, but on a Package row (whose own Grid had NO Background
            // at all before this fix, unlike Group's).
            var cell = FindCellControl(view, vm.Rows.IndexOf(packageRow), LedgerColumnWidths.SoDuTuIndex);
            cell.Focusable.ShouldBeFalse();

            Click(window, cell);

            vm.SelectedRow.ShouldBeSameAs(packageRow);
            window.Close();
        });
    }

    [Fact]
    public void Clicking_a_non_focusable_placeholder_cell_in_an_Entry_row_still_selects_it()
    {
        HeadlessTestSession.Run(() =>
        {
            var (window, view, vm) = CreateLedger(BuildTwoGroupsTwoPackagesOneEntryFixture());
            var entryRow = vm.Rows.Single(r => r.Kind == RowKind.Entry);

            // Entry rows don't even have a TextBlock at this column (no computed subtotal of
            // their own) — this is a completely bare gridCellBorder placeholder, the
            // strictest version of "non-focusable part of a row".
            var cell = FindCellControl(view, vm.Rows.IndexOf(entryRow), LedgerColumnWidths.SoDuTuIndex);
            cell.Focusable.ShouldBeFalse();
            cell.GetVisualDescendants().OfType<TextBlock>().ShouldBeEmpty();

            Click(window, cell);

            vm.SelectedRow.ShouldBeSameAs(entryRow);
            window.Close();
        });
    }

    // ===== FIX 2: a money cell re-formats with "." grouping once it loses focus =====

    [Theory]
    [InlineData(2, LedgerColumnWidths.GiaTriHdIndex)]   // Package: Giá trị HĐ
    [InlineData(2, LedgerColumnWidths.PhuLucIndex)]     // Package: Phụ lục HĐ
    [InlineData(3, LedgerColumnWidths.TamUngIndex)]     // Entry: Tạm ứng
    [InlineData(3, LedgerColumnWidths.KlhtIndex)]       // Entry: KLHT
    [InlineData(3, LedgerColumnWidths.ThuHoiIndex)]     // Entry: Thu hồi
    public void Typing_digits_into_a_money_cell_then_losing_focus_reformats_with_dot_grouping(int rowIndex, int column)
    {
        HeadlessTestSession.Run(() =>
        {
            var (window, view, vm) = CreateLedger(BuildOneOfEachRowKindFixture());

            var moneyBox = FindCellTextBox(view, rowIndex, column);
            var elsewhere = FindCellTextBox(view, 1, LedgerColumnWidths.NoiDungIndex); // the Nhóm 1 name box

            moneyBox.Focus();
            moneyBox.SelectAll(); // mirrors TryFocusEditable's own Focus()+SelectAll() pairing
            // Real simulated keystrokes (not a raw `.Text =` assignment) — this is what
            // actually reproduces the TwoWay re-entrancy drop: the TextBox's own text-input
            // pipeline is what nests the source-property write inside the still-running
            // target-update, which is the exact call stack the bug depends on.
            window.KeyTextInput("123456789");
            Dispatcher.UIThread.RunJobs();

            elsewhere.Focus(); // moves focus away -> LostFocus on moneyBox
            Dispatcher.UIThread.RunJobs();

            moneyBox.Text.ShouldBe(MoneyText.Format(123_456_789m));

            var row = vm.Rows[rowIndex];
            var parsedValue = column switch
            {
                LedgerColumnWidths.GiaTriHdIndex => row.ContractValue,
                LedgerColumnWidths.PhuLucIndex => row.ContractAnnexValue,
                LedgerColumnWidths.TamUngIndex => row.Advance,
                LedgerColumnWidths.KlhtIndex => row.PaymentKLHT,
                LedgerColumnWidths.ThuHoiIndex => row.RecoveredAdvance,
                _ => throw new InvalidOperationException("Unexpected column under test.")
            };
            parsedValue.ShouldBe(123_456_789m);

            window.Close();
        });
    }

    [Fact]
    public void Typing_an_alternate_valid_date_format_then_losing_focus_reformats_to_dd_MM_yyyy()
    {
        // FinanceCellParsing.ParseDate also accepts "yyyy-MM-dd" (not just the canonical
        // "dd/MM/yyyy" the getter re-emits), so this exercises the SAME re-entrancy-dropped-
        // echo bug as the money cells: parsing succeeds, but the getter's reformatted string
        // ("01/06/2026") differs from what's on screen ("2026-06-01") until something re-pulls
        // DateText into the TextBox.
        HeadlessTestSession.Run(() =>
        {
            var (window, view, vm) = CreateLedger(BuildOneOfEachRowKindFixture());
            var entryRow = vm.Rows.Single(r => r.Kind == RowKind.Entry);

            var dateBox = FindCellTextBox(view, vm.Rows.IndexOf(entryRow), LedgerColumnWidths.Ngay);
            var elsewhere = FindCellTextBox(view, 1, LedgerColumnWidths.NoiDungIndex);

            dateBox.Focus();
            dateBox.SelectAll();
            window.KeyTextInput("2026-06-01"); // real simulated keystrokes — see money test above
            Dispatcher.UIThread.RunJobs();

            elsewhere.Focus();
            Dispatcher.UIThread.RunJobs();

            dateBox.Text.ShouldBe("01/06/2026");
            entryRow.Date.ShouldBe(new DateOnly(2026, 6, 1));

            window.Close();
        });
    }
}
