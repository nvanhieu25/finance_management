using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ConstructionFinance.Desktop.ViewModels.Projects;
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Desktop.Views.Projects;

public partial class ProjectDetailView : UserControl
{
    public ProjectDetailView() => AvaloniaXamlLoader.Load(this);

    private async void OnStatusChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ProjectDetailViewModel vm && sender is ComboBox cb
            && cb.SelectedItem is ProjectStatus newStatus
            && newStatus != vm.Project.Status)
        {
            await vm.ChangeStatusAsync(newStatus);
        }
    }
}
