using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using ConstructionFinance.Desktop.ViewModels.Finance;

namespace ConstructionFinance.Desktop.Views.Finance;

public partial class FinanceSummaryView : UserControl
{
    public FinanceSummaryView() => AvaloniaXamlLoader.Load(this);

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is FinanceSummaryViewModel vm && vm.OpenProjectCommand.CanExecute(null))
            vm.OpenProjectCommand.Execute(null);
    }
}
