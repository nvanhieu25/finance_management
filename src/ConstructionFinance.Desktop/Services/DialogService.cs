using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.ViewModels.Common;
using Serilog;
using FluentAvalonia.UI.Controls;

namespace ConstructionFinance.Desktop.Services;

public class DialogService : IDialogService
{
    public async Task<bool?> ShowDialogAsync<TViewModel>(TViewModel viewModel) where TViewModel : class
    {
        var locator = new ViewLocator();
        var content = locator.Build(viewModel) ?? new TextBlock { Text = "(no view)" };
        content.DataContext = viewModel;

        var window = GetMainWindow();
        var dialog = new ContentDialog
        {
            Title = (viewModel as IHasDialogTitle)?.DialogTitle ?? "",
            Content = content,
            PrimaryButtonText = "Lưu",
            CloseButtonText = "Hủy"
        };
        // FluentAvalonia caps content at ContentDialogMaxWidth (~548). Content wider than
        // that (e.g. the import preview grid) gets centered and clipped off both edges.
        // Raise the cap so wide views render fully instead of overflowing off-screen.
        dialog.Resources["ContentDialogMaxWidth"] = 1200.0;
        dialog.Resources["ContentDialogMaxHeight"] = 900.0;
        ContentDialogResult result;
        if (window is not null)
        {
            result = await dialog.ShowAsync(window);
        }
        else
        {
            Log.Warning("ShowDialogAsync: MainWindow is null, calling ShowAsync() without window");
            result = await dialog.ShowAsync();
        }
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

    public async Task<string?> PickOpenFileAsync(string title, IReadOnlyList<string> extensions)
    {
        var storage = GetStorageProvider();
        if (storage is null)
        {
            Log.Warning("PickOpenFileAsync: StorageProvider is null — cannot open file picker");
            return null;
        }

        Log.Information("PickOpenFileAsync: opening picker, extensions={Ext}", string.Join(",", extensions));

        var types = extensions.Select(ext => new FilePickerFileType(ext)
        {
            Patterns = new[] { ext.StartsWith('.') ? $"*{ext}" : $"*.{ext}" },
            MimeTypes = ext.ToLowerInvariant() switch
            {
                ".xlsx" => new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
                ".xlsm" => new[] { "application/vnd.ms-excel.sheet.macroEnabled.12" },
                _ => null
            }
        }).ToList();

        try
        {
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = types
            });
            var file = files.FirstOrDefault();
            Log.Information("PickOpenFileAsync: picked={Path}", file?.Path.LocalPath ?? "(none)");
            return file?.Path.LocalPath;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PickOpenFileAsync: file picker threw");
            return null;
        }
    }

    public async Task<string?> PickSaveFileAsync(string title, string defaultFileName, IReadOnlyList<string> extensions)
    {
        var storage = GetStorageProvider();
        if (storage is null) return null;

        var types = extensions.Select(ext => new FilePickerFileType(title)
        {
            Patterns = new[] { ext.StartsWith('.') ? $"*{ext}" : $"*.{ext}" }
        }).ToList();

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName,
            FileTypeChoices = types
        });
        return file?.Path.LocalPath;
    }

    private static Avalonia.Controls.Window? GetMainWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }

    private static IStorageProvider? GetStorageProvider()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow?.StorageProvider;
        return null;
    }
}

public interface IHasDialogTitle
{
    string DialogTitle { get; }
}
