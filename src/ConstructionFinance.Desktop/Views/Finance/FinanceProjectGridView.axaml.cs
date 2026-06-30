using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;

namespace ConstructionFinance.Desktop.Views.Finance;

public partial class FinanceProjectGridView : UserControl
{
    public FinanceProjectGridView() => AvaloniaXamlLoader.Load(this);

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // Tunnel so we react before the TextBox consumes the key.
        AddHandler(KeyDownEvent, OnGridKeyDown, RoutingStrategies.Tunnel);
    }

    private async void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not FinanceProjectGridViewModel vm) return;
        if (e.Source is not Control source) return;

        var (rowIndex, colIndex) = LocateCell(source);
        if (rowIndex < 0) return;

        // Ctrl+V → paste a TSV block from the active cell.
        if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null) return;
            var text = await clipboard.GetTextAsync();
            if (string.IsNullOrEmpty(text)) return;

            var block = ClipboardGridParser.Parse(text);
            vm.ApplyPaste(rowIndex, colIndex, block);
            e.Handled = true;
            return;
        }

        // Enter / arrows → vertical cell movement (Tab handled by native focus order).
        switch (e.Key)
        {
            case Key.Enter:
            case Key.Down:
                if (e.Key == Key.Enter && rowIndex == vm.Lines.Count - 1)
                    vm.AddRowCommand.Execute(null);
                MoveFocus(rowIndex + 1, colIndex);
                e.Handled = true;
                break;
            case Key.Up:
                MoveFocus(rowIndex - 1, colIndex);
                e.Handled = true;
                break;
        }
    }

    /// Find which (row, column) a focused cell belongs to within RowsHost.
    private (int row, int col) LocateCell(Control source)
    {
        var cell = source as TextBox ?? source.FindAncestorOfType<TextBox>();
        if (cell is null) return (-1, -1);
        var col = Grid.GetColumn(cell);

        var rowGrid = cell.FindAncestorOfType<Grid>();
        if (rowGrid is null) return (-1, -1);
        var container = rowGrid.FindAncestorOfType<ContentPresenter>();
        if (container is null) return (-1, -1);
        var rowsHost = this.FindControl<ItemsControl>("RowsHost")!;
        var row = rowsHost.IndexFromContainer(container);
        return (row, col);
    }

    /// Focus the cell at (row, col), if it exists and is an editable TextBox.
    private void MoveFocus(int row, int col)
    {
        if (row < 0) return;
        var container = this.FindControl<ItemsControl>("RowsHost")!.ContainerFromIndex(row);
        if (container is null) return;
        var grid = container.FindDescendantOfType<Grid>();
        if (grid is null) return;
        foreach (var child in grid.Children)
        {
            if (child is TextBox tb && Grid.GetColumn(tb) == col)
            {
                tb.Focus();
                tb.SelectAll();
                return;
            }
        }
    }
}
