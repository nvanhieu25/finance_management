using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using ConstructionFinance.Desktop.ViewModels.Finance;

namespace ConstructionFinance.Desktop.Views.Finance;

public partial class WorkbookSummaryView : UserControl
{
    public WorkbookSummaryView() => AvaloniaXamlLoader.Load(this);

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is WorkbookSummaryViewModel vm && vm.SelectedRow is { ProjectId: > 0 } row)
            vm.ActivateProject(row.ProjectId);
    }
}
