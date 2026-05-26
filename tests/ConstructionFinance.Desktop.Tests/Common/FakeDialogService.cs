using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.Tests.Common;

public class FakeDialogService : IDialogService
{
    public List<object> OpenedDialogs { get; } = new();
    public List<string> ShownMessages { get; } = new();
    public bool ConfirmResult { get; set; } = true;
    public bool? DialogResult { get; set; } = true;

    public Task<bool?> ShowDialogAsync<TViewModel>(TViewModel viewModel) where TViewModel : class
    {
        OpenedDialogs.Add(viewModel);
        return Task.FromResult(DialogResult);
    }

    public Task<bool> ConfirmAsync(string title, string message)
    {
        ShownMessages.Add($"CONFIRM: {title} — {message}");
        return Task.FromResult(ConfirmResult);
    }

    public Task ShowMessageAsync(string title, string message)
    {
        ShownMessages.Add($"MSG: {title} — {message}");
        return Task.CompletedTask;
    }
}
