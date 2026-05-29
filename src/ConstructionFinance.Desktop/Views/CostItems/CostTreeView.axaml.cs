using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ConstructionFinance.Desktop.ViewModels.CostItems;

namespace ConstructionFinance.Desktop.Views.CostItems;

public partial class CostTreeView : UserControl
{
    public CostTreeView() => AvaloniaXamlLoader.Load(this);

    private async void OnCellLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: CostTreeNode node } &&
            DataContext is CostTreeViewModel vm)
        {
            await vm.ApplyEditAsync(node);
        }
    }
}
