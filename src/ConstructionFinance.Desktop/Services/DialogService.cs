using Avalonia.Controls;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.ViewModels.Common;
using FluentAvalonia.UI.Controls;

namespace ConstructionFinance.Desktop.Services;

public class DialogService : IDialogService
{
    public async Task<bool?> ShowDialogAsync<TViewModel>(TViewModel viewModel) where TViewModel : class
    {
        var locator = new ViewLocator();
        var content = locator.Build(viewModel) ?? new TextBlock { Text = "(no view)" };
        content.DataContext = viewModel;

        var dialog = new ContentDialog
        {
            Title = (viewModel as IHasDialogTitle)?.DialogTitle ?? "",
            Content = content,
            PrimaryButtonText = "Lưu",
            CloseButtonText = "Hủy"
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var vm = new ConfirmDialogViewModel { Title = title, Message = message };
        var content = new Views.Common.ConfirmDialog { DataContext = vm };
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = vm.ConfirmText,
            CloseButtonText = vm.CancelText,
            DefaultButton = ContentDialogButton.Primary
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            CloseButtonText = "Đóng"
        };
        await dialog.ShowAsync();
    }
}

public interface IHasDialogTitle
{
    string DialogTitle { get; }
}
