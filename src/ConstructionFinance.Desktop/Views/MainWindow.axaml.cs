using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ConstructionFinance.Desktop.ViewModels;
using FluentAvalonia.UI.Controls;
using Serilog;

namespace ConstructionFinance.Desktop.Views;

public partial class MainWindow : Window
{
    // NavigationView.SelectedItem is the single source of truth for the current
    // screen. But FluentAvalonia's NavigationView raises SelectionChanged with
    // transient values while it attaches and realizes its items — including
    // several events AFTER the pane's Loaded fires as it finishes measuring with
    // real data. We swallow every one of those until we deliberately land, then
    // navigate exactly once from the landing selection.
    private bool _navInitialized;
    private string? _currentTag;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        var nav = this.FindControl<NavigationView>("Nav");
        if (nav is not null)
        {
            nav.SelectionChanged += OnNavSelectionChanged;
            nav.Loaded += OnNavLoaded;
        }
    }

    private void OnNavLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not NavigationView nav) return;
        nav.Loaded -= OnNavLoaded;

        // FA keeps raising SelectionChanged after Loaded as it realizes items
        // with real data. Defer the landing to Background priority so all that
        // churn drains first (ignored while _navInitialized is false), then land
        // exactly once.
        Dispatcher.UIThread.Post(async () =>
        {
            nav.SelectedItem = this.FindControl<NavigationViewItem>("NavFinanceWorkbook");
            _navInitialized = true;
            if (DataContext is MainWindowViewModel vm
                && nav.SelectedItem is NavigationViewItem item
                && item.Tag is string tag)
            {
                _currentTag = tag;
                // async void continuation: an unhandled exception here would crash
                // the app (no global handler registered), so degrade gracefully
                // like the old InitializeAsync did.
                try { await vm.NavigateToAsync(tag); }
                catch (Exception ex) { Log.Warning(ex, "Navigation to {Tag} failed", tag); }
            }
        }, DispatcherPriority.Background);
    }

    private async void OnNavSelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
    {
        if (!_navInitialized) return;
        if (DataContext is MainWindowViewModel vm && e.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            // Skip redundant re-navigation to the screen we're already on.
            if (tag == _currentTag) return;
            _currentTag = tag;
            try { await vm.NavigateToAsync(tag); }
            catch (Exception ex) { Log.Warning(ex, "Navigation to {Tag} failed", tag); }
        }
    }
}
