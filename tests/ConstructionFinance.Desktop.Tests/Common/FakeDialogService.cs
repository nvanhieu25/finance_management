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

    public string? PickOpenFileResult { get; set; }
    public string? PickSaveFileResult { get; set; }

    public Task<string?> PickOpenFileAsync(string title, IReadOnlyList<string> extensions) =>
        Task.FromResult(PickOpenFileResult);

    /// Mỗi lần mở hộp thoại chọn nơi lưu để lại một dòng ở đây, để test xem được tên tệp gợi ý và đuôi tệp.
    public List<SaveFileRequest> SaveFileRequests { get; } = new();

    /// Khác null thì hộp thoại chọn nơi lưu hỏng (trả một Task lỗi, như bộ chọn tệp của hệ điều hành khi nó ném).
    public Exception? PickSaveFileFailure { get; set; }

    public Task<string?> PickSaveFileAsync(string title, string defaultFileName, IReadOnlyList<string> extensions)
    {
        SaveFileRequests.Add(new SaveFileRequest(title, defaultFileName, extensions));
        return PickSaveFileFailure is null
            ? Task.FromResult(PickSaveFileResult)
            : Task.FromException<string?>(PickSaveFileFailure);
    }
}

public sealed record SaveFileRequest(string Title, string DefaultFileName, IReadOnlyList<string> Extensions);
