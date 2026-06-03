namespace ConstructionFinance.Desktop.Services;

public interface IDialogService
{
    /// Open a content dialog whose body is the View resolved from `viewModel`.
    /// Returns the dialog result — `true` if user confirmed, `false`/`null` if cancelled.
    Task<bool?> ShowDialogAsync<TViewModel>(TViewModel viewModel) where TViewModel : class;

    /// Show a simple Yes/No confirmation. Returns true if user confirms.
    Task<bool> ConfirmAsync(string title, string message);

    /// Show a message dialog (info/error).
    Task ShowMessageAsync(string title, string message);

    /// Open file picker. Returns full path or null if cancelled.
    Task<string?> PickOpenFileAsync(string title, IReadOnlyList<string> extensions);

    /// Save file picker. Returns full path or null if cancelled.
    Task<string?> PickSaveFileAsync(string title, string defaultFileName, IReadOnlyList<string> extensions);
}
