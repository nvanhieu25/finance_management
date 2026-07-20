using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ConstructionFinance.Desktop.ViewModels;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
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
    private bool _forceClose;

    private readonly Services.IDialogService _dialog =
        App.Host!.Services.GetRequiredService<Services.IDialogService>();

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

        Closing += OnWindowClosing;
    }

    private void OnUserInteraction(object? sender, RoutedEventArgs e) => _userInteracted = true;

    // Excel-style guard: never let unsaved project pages die silently.
    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_forceClose) return;
        if (DataContext is not MainWindowViewModel vm) return;
        if (vm.CurrentContent is not ViewModels.Finance.FinanceWorkbookViewModel wb || !wb.IsDirty) return;

        e.Cancel = true;
        var leave = await _dialog.ConfirmAsync("Còn thay đổi chưa lưu",
            "Có dự án chưa lưu. Thoát mà không lưu?");
        if (!leave) return;

        _forceClose = true;
        Close();
    }

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
        if (DataContext is not MainWindowViewModel vm
            || e.InvokedItemContainer is not NavigationViewItem item
            || item.Tag is not string tag)
            return;

        // Excel-style guard: leaving a dirty workbook for another screen would drop
        // unsaved project edits (the workbook VM is transient — nav-away discards it).
        // Prompt first; if the user stays, snap the nav highlight back to the workbook.
        if (tag != "FinanceWorkbook"
            && vm.CurrentContent is ViewModels.Finance.FinanceWorkbookViewModel { IsDirty: true }
            && sender is NavigationView nav)
        {
            var leave = await _dialog.ConfirmAsync("Còn thay đổi chưa lưu",
                "Có dự án chưa lưu. Rời khỏi màn Tài chính dự án mà không lưu?");
            if (!leave)
            {
                nav.SelectedItem = this.FindControl<NavigationViewItem>("NavFinanceWorkbook");
                return;
            }
        }

        try { await vm.NavigateToAsync(tag); }
        catch (Exception ex) { Log.Warning(ex, "Navigation to {Tag} failed", tag); }
    }
}
