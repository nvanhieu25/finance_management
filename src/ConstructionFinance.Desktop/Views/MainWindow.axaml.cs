using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ConstructionFinance.Desktop.ViewModels;
using FluentAvalonia.UI.Controls;

namespace ConstructionFinance.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        var nav = this.FindControl<NavigationView>("Nav");
        if (nav is not null)
            nav.SelectionChanged += OnNavSelectionChanged;
    }

    private async void OnNavSelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && e.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            await vm.NavigateToAsync(tag);
        }
    }
}
