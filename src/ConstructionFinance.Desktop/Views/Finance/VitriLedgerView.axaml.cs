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

        WireColumnGrips();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
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

    /// Find which (row, column) a focused cell belongs to within RowsHost. Walks up
    /// from the focused control (TextBox, or a ComboBox nested inside a gridline
    /// Border) to the child that is a DIRECT child of the row's Grid — that child
    /// carries the real Grid.Column, even when it's the Border wrapping the control.
    private (int row, int col) LocateCell(Control source)
    {
        Control? rowChild = null;
        Control? walker = source;
        while (walker is not null)
        {
            if (walker.Parent is Grid { } grid && grid.Classes.Contains("gridRow"))
            {
                rowChild = walker;
                break;
            }
            walker = walker.Parent as Control;
        }
        if (rowChild is null) return (-1, -1);

        var col = Grid.GetColumn(rowChild);
        var rowGrid = rowChild.GetVisualParent() as Grid;
        var container = rowGrid?.FindAncestorOfType<ContentPresenter>();
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
}
