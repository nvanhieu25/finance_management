using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ConstructionFinance.Desktop.ViewModels;
using FluentAvalonia.UI.Controls;
using Serilog;

namespace ConstructionFinance.Desktop.Views;

public partial class MainWindow : Window
{
    // FluentAvalonia raises BOTH SelectionChanged and ItemInvoked for its own
    // transient selections during startup, landing us on random screens. Those
    // are never driven by real input, so we only honor a nav invoke AFTER the
    // user has actually pressed the pointer or a key. The initial landing is an
    // explicit one-time navigation below, independent of nav events.
    private bool _userInteracted;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);

        AddHandler(PointerPressedEvent, OnUserInteraction, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnUserInteraction, RoutingStrategies.Tunnel);

        var nav = this.FindControl<NavigationView>("Nav");
        if (nav is not null)
        {
            nav.ItemInvoked += OnNavItemInvoked;
            nav.Loaded += OnNavLoaded;
        }
    }

    private void OnUserInteraction(object? sender, RoutedEventArgs e) => _userInteracted = true;

    private async void OnNavLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not NavigationView nav) return;
        nav.Loaded -= OnNavLoaded;

        nav.SelectedItem = this.FindControl<NavigationViewItem>("NavFinanceWorkbook");
        if (DataContext is MainWindowViewModel vm)
        {
            try { await vm.NavigateToAsync("FinanceWorkbook"); }
            catch (Exception ex) { Log.Warning(ex, "Initial workbook navigation failed"); }
        }
    }

    private async void OnNavItemInvoked(object? sender, NavigationViewItemInvokedEventArgs e)
    {
        if (!_userInteracted) return;   // ignore FA's startup churn (no real input yet)
        if (DataContext is MainWindowViewModel vm
            && e.InvokedItemContainer is NavigationViewItem item
            && item.Tag is string tag)
        {
            try { await vm.NavigateToAsync(tag); }
            catch (Exception ex) { Log.Warning(ex, "Navigation to {Tag} failed", tag); }
        }
    }
}
