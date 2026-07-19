using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ConstructionFinance.Desktop.ViewModels;
using FluentAvalonia.UI.Controls;
using Serilog;

namespace ConstructionFinance.Desktop.Views;

public partial class MainWindow : Window
{
    // NavigationView.SelectedItem is the single source of truth for the current
    // screen. But FluentAvalonia's NavigationView raises SelectionChanged with
    // transient values while it attaches and realizes its items — before we can
    // set the landing selection. We swallow those early events (they would race
    // us onto the wrong screen) until the pane is loaded, then set the landing
    // selection deterministically and navigate exactly once from it.
    private bool _navInitialized;

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

    private async void OnNavLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not NavigationView nav) return;
        nav.Loaded -= OnNavLoaded;

        // Items are realized by now, so this resolves to the real item and wins
        // over any auto-selection FluentAvalonia performed during attach.
        nav.SelectedItem = this.FindControl<NavigationViewItem>("NavFinanceWorkbook");
        _navInitialized = true;

        // The assignment above may or may not have raised SelectionChanged (it
        // was swallowed either way, since _navInitialized flipped after it), so
        // drive the one landing navigation explicitly from the current selection.
        if (DataContext is MainWindowViewModel vm
            && nav.SelectedItem is NavigationViewItem item
            && item.Tag is string tag)
        {
            // async void: an unhandled exception here would crash the app (no
            // global handler registered), so degrade gracefully like the old
            // InitializeAsync did.
            try { await vm.NavigateToAsync(tag); }
            catch (Exception ex) { Log.Warning(ex, "Navigation to {Tag} failed", tag); }
        }
    }

    private async void OnNavSelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
    {
        if (!_navInitialized) return;
        if (DataContext is MainWindowViewModel vm && e.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            try { await vm.NavigateToAsync(tag); }
            catch (Exception ex) { Log.Warning(ex, "Navigation to {Tag} failed", tag); }
        }
    }
}
