using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;

namespace ConstructionFinance.Desktop.Views.Finance;

public partial class VitriLedgerView : UserControl
{
    // Tracked so OnUnloaded can detach — Thumbs are re-templated per Vị trí tab
    // instance but this View instance itself is reused as tabs are switched away
    // from and back to, so handlers must not pile up across (re)loads.
    private readonly List<Thumb> _wiredGrips = new();

    public VitriLedgerView() => AvaloniaXamlLoader.Load(this);

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // Tunnel so we react before the TextBox/ComboBox consumes the key.
        AddHandler(KeyDownEvent, OnGridKeyDown, RoutingStrategies.Tunnel);
        // GotFocus already bubbles by default — covers every editable cell (TextBox/
        // ComboBox). PointerPressed is added at Tunnel so it still fires even if some
        // control marks the event Handled during Bubble — covers a click on a
        // non-focusable part of a row (an empty gridline Border, a read-only computed
        // TextBlock cell, ...) where nothing ever takes focus.
        AddHandler(GotFocusEvent, OnGridGotFocus, RoutingStrategies.Bubble);
        AddHandler(PointerPressedEvent, OnGridPointerPressed, RoutingStrategies.Tunnel);

        WireColumnGrips();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        // This View instance is reused as Vị trí tabs are switched away from and back to
        // (see _wiredGrips remarks below), so every handler wired in OnLoaded must be
        // unwired here or it piles up across (re)loads.
        RemoveHandler(KeyDownEvent, OnGridKeyDown);
        RemoveHandler(GotFocusEvent, OnGridGotFocus);
        RemoveHandler(PointerPressedEvent, OnGridPointerPressed);

        foreach (var thumb in _wiredGrips)
            thumb.DragDelta -= OnColumnGripDragDelta;
        _wiredGrips.Clear();

        base.OnUnloaded(e);
    }

    /// Part A (Excel-style manual column resize): every header cell has a thin Thumb
    /// grip (Classes="colGrip", Tag="<column index 0..13>") on its right edge. Dragging
    /// one adds the horizontal delta to that column's pixel width in the shared
    /// VitriLedgerViewModel.ColumnWidths model — since the header, TỔNG CỘNG row, and
    /// every RowKind row template all bind their ColumnDefinitions to that SAME model,
    /// they resize together automatically; no per-grid code needed here beyond this.
    private void WireColumnGrips()
    {
        var headerGrid = this.FindControl<Grid>("HeaderColumnsGrid");
        if (headerGrid is null) return;

        foreach (var thumb in headerGrid.GetVisualDescendants().OfType<Thumb>())
        {
            thumb.DragDelta += OnColumnGripDragDelta;
            _wiredGrips.Add(thumb);
        }
    }

    private void OnColumnGripDragDelta(object? sender, VectorEventArgs e)
    {
        if (sender is not Thumb thumb) return;
        if (DataContext is not VitriLedgerViewModel vm) return;
        if (thumb.Tag is not string tagText || !int.TryParse(tagText, out var columnIndex)) return;

        vm.ColumnWidths.Resize(columnIndex, e.Vector.X);
    }

    private async void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not VitriLedgerViewModel vm) return;
        if (e.Source is not Control source) return;

        var (rowIndex, colIndex) = LocateCell(source);
        if (rowIndex < 0 || rowIndex >= vm.Rows.Count) return;

        // Ctrl+V → paste a TSV block, Entry rows only (ApplyPaste itself is a no-op
        // for any other Kind, but skip the clipboard round-trip when we already know).
        if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (vm.Rows[rowIndex].Kind != RowKind.Entry) return;

            e.Handled = true;   // mark handled before the await so the TextBox doesn't also paste
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null) return;
            var text = await clipboard.GetTextAsync();
            if (string.IsNullOrEmpty(text)) return;

            var block = ClipboardGridParser.Parse(text);
            vm.ApplyPaste(vm.Rows[rowIndex], colIndex, block);
            return;
        }

        // Enter / arrows → vertical cell movement (Tab handled by native focus order).
        switch (e.Key)
        {
            case Key.Enter:
            case Key.Down:
                MoveFocus(rowIndex + 1, colIndex);
                e.Handled = true;
                break;
            case Key.Up:
                MoveFocus(rowIndex - 1, colIndex);
                e.Handled = true;
                break;
        }
    }

    // FIX (7B review, Important #1): SelectedRow used to be written ONLY by AddPackage/
    // AddEntry/DeleteSelected themselves — nothing in the View ever set it from a click or
    // a focus change, so once a Vị trí had more than one nhóm/gói the toolbar's "Thêm gói"/
    // "Thêm đợt"/"Xóa dòng đang chọn" (all bound to SelectedRow) silently no-op'd, with no
    // way at all to delete an existing row. Both handlers below funnel into the same
    // LocateCell used by arrow-key navigation, so "which row" is resolved identically
    // everywhere in this file.
    private void OnGridGotFocus(object? sender, GotFocusEventArgs e) => SelectRowFromSource(e.Source);
    private void OnGridPointerPressed(object? sender, PointerPressedEventArgs e) => SelectRowFromSource(e.Source);

    private void SelectRowFromSource(object? source)
    {
        if (DataContext is not VitriLedgerViewModel vm) return;
        if (source is not Control control) return;

        var (rowIndex, _) = LocateCell(control);
        if (rowIndex < 0 || rowIndex >= vm.Rows.Count) return;
        vm.SelectedRow = vm.Rows[rowIndex];
    }

    /// Find which (row, column) a focused/clicked cell belongs to within RowsHost. Walks up
    /// from `source` (a TextBox, a ComboBox nested inside a gridline Border, or — a pointer
    /// click that landed on an empty gridCellBorder placeholder with no Background of its
    /// own — the row's own Grid itself, since the click falls through to it) to the child
    /// that is a DIRECT child of the row's Grid — that child carries the real Grid.Column,
    /// even when it's the Border wrapping the control. Column comes back -1 when `source`
    /// IS the row Grid (no specific cell was hit, just the row's background) — callers that
    /// only need the row (row selection) ignore it; callers that need a real column
    /// (KeyDown navigation/paste) never see this case in practice since a bare Grid is never
    /// focusable/keyboard-sourced.
    private (int row, int col) LocateCell(Control source)
    {
        Control? walker = source;
        while (walker is not null)
        {
            if (walker is Grid { } selfGrid && selfGrid.Classes.Contains("gridRow"))
                return ResolveRow(selfGrid, -1);

            if (walker.Parent is Grid { } parentGrid && parentGrid.Classes.Contains("gridRow"))
                return ResolveRow(parentGrid, Grid.GetColumn(walker));

            walker = walker.Parent as Control;
        }
        return (-1, -1);
    }

    private (int row, int col) ResolveRow(Grid rowGrid, int col)
    {
        var container = rowGrid.FindAncestorOfType<ContentPresenter>();
        if (container is null) return (-1, -1);

        var rowsHost = this.FindControl<ItemsControl>("RowsHost")!;
        var row = rowsHost.IndexFromContainer(container);
        return (row, col);
    }

    /// Focus the editable control at (row, col), if the row/column combination has
    /// one — Group/Package/Total rows don't have a control at every column, and this
    /// is a no-op then (no cross-row-kind fallback in this pass).
    private void MoveFocus(int row, int col)
    {
        if (row < 0) return;
        var rowsHost = this.FindControl<ItemsControl>("RowsHost")!;
        var container = rowsHost.ContainerFromIndex(row);
        if (container is null) return;
        var rowGrid = container.FindDescendantOfType<Grid>();
        if (rowGrid is null) return;

        foreach (var child in rowGrid.Children)
        {
            if (child is not Control c || Grid.GetColumn(c) != col) continue;
            if (TryFocusEditable(c)) return;
        }
    }

    private static bool TryFocusEditable(Control cell)
    {
        switch (cell)
        {
            case TextBox tb:
                tb.Focus();
                tb.SelectAll();
                return true;
            case ComboBox cb:
                cb.Focus();
                return true;
            default:
                var nestedBox = cell.FindDescendantOfType<TextBox>();
                if (nestedBox is not null)
                {
                    nestedBox.Focus();
                    nestedBox.SelectAll();
                    return true;
                }
                var nestedCombo = cell.FindDescendantOfType<ComboBox>();
                if (nestedCombo is not null)
                {
                    nestedCombo.Focus();
                    return true;
                }
                return false;
        }
    }

    // FIX (7B review, Important #2): the money facades (AdvanceText/KlhtText/RecoveredText/
    // ContractValueText/ContractAnnexText) push their reformatted "1.234.567" string back via
    // OnPropertyChanged from inside their OWN setter's call stack — Avalonia's TwoWay binding
    // re-entrancy guard silently drops that echo, so the TextBox keeps showing the raw digits
    // the user typed for the rest of the session even though the underlying decimal parsed
    // fine. Spec §6.2 requires the "." grouping to reappear once the cell loses focus, so
    // this handler just re-pulls the facade's CURRENT value into the TextBox's Text — a
    // fresh read outside that call stack, so the drop doesn't apply. DateText has the exact
    // same problem for its OTHER accepted input format ("yyyy-MM-dd" — FinanceCellParsing.
    // ParseDate accepts it, but the getter always re-emits "dd/MM/yyyy", so that echo gets
    // dropped too; a headless test proved this fails identically before this fix), so it is
    // wired to the same handler. Every affected TextBox across the Entry/Package templates
    // is wired to this one handler, keyed by its Tag (the facade's own property name) — same
    // Tag-driven-dispatch pattern already used by the column-resize grips above.
    private void OnFacadeCellLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: LedgerRowViewModel row } textBox) return;
        if (textBox.Tag is not string facade) return;

        textBox.Text = facade switch
        {
            nameof(LedgerRowViewModel.DateText) => row.DateText,
            nameof(LedgerRowViewModel.AdvanceText) => row.AdvanceText,
            nameof(LedgerRowViewModel.KlhtText) => row.KlhtText,
            nameof(LedgerRowViewModel.RecoveredText) => row.RecoveredText,
            nameof(LedgerRowViewModel.ContractValueText) => row.ContractValueText,
            nameof(LedgerRowViewModel.ContractAnnexText) => row.ContractAnnexText,
            _ => textBox.Text
        };
    }
}
