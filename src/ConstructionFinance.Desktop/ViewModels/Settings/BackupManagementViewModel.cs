using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Infrastructure.Backup;

namespace ConstructionFinance.Desktop.ViewModels.Settings;

public partial class BackupManagementViewModel : ViewModelBase
{
    private const string CurrentAppVersion = "1.0.0";
    private const string CurrentDbSchemaVersion = "20260519_001";

    private readonly IBackupService _backup;
    private readonly IRestoreService _restore;
    private readonly IDialogService _dialog;

    public BackupManagementViewModel(IBackupService backup, IRestoreService restore, IDialogService dialog)
    {
        _backup = backup;
        _restore = restore;
        _dialog = dialog;
    }

    public ObservableCollection<BackupFileInfo> Backups { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreSelectedCommand))]
    private BackupFileInfo? _selectedBackup;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;

    // Option A: do I/O via Task.Run, then await back on calling thread to mutate collection.
    // This avoids Avalonia Dispatcher.UIThread.Post which requires an Avalonia app context
    // (unavailable in xUnit tests). The await after Task.Run resumes on the original
    // synchronisation context (typically the UI thread in the real app, test thread in tests).
    public async Task LoadAsync()
    {
        var list = await Task.Run(() => _backup.ListBackups(AppPaths.BackupsFolder));
        Backups.Clear();
        foreach (var b in list) Backups.Add(b);
    }

    [RelayCommand]
    private async Task BackupNowAsync()
    {
        IsBusy = true;
        try
        {
            var manifest = BuildManifest(backupType: "manual", description: null);
            var result = await _backup.CreateBackupAsync(AppPaths.DatabaseFile, AppPaths.AttachmentsFolder, AppPaths.BackupsFolder, manifest);
            Backups.Insert(0, result);
            StatusMessage = $"Đã backup: {result.FileName}";
            await _dialog.ShowMessageAsync("Backup", $"Đã backup thành công: {result.FileName}");
        }
        catch (Exception ex)
        {
            await _dialog.ShowMessageAsync("Lỗi", ex.Message);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RestoreSelectedAsync()
    {
        if (SelectedBackup is null) return;
        var confirm1 = await _dialog.ConfirmAsync("Restore",
            $"Bạn có chắc muốn restore từ {SelectedBackup.FileName}?\nDữ liệu hiện tại sẽ được backup tự động (pre_restore) trước khi thay thế.");
        if (!confirm1) return;
        var confirm2 = await _dialog.ConfirmAsync("Xác nhận lần cuối",
            "Hành động này KHÔNG THỂ undo nếu chỉ dựa vào memory. Restore ngay?");
        if (!confirm2) return;

        IsBusy = true;
        try
        {
            await _restore.RestoreAsync(SelectedBackup.FullPath, AppPaths.DatabaseFile, AppPaths.AttachmentsFolder, AppPaths.BackupsFolder, CurrentDbSchemaVersion);
            await _dialog.ShowMessageAsync("Restore", "Restore thành công. Vui lòng khởi động lại app để dữ liệu mới có hiệu lực.");
        }
        catch (Exception ex)
        {
            await _dialog.ShowMessageAsync("Lỗi Restore", ex.Message);
        }
        finally { IsBusy = false; await LoadAsync(); }
    }

    private bool HasSelection() => SelectedBackup is not null;

    private static BackupManifest BuildManifest(string backupType, string? description) => new(
        FormatVersion: "1.0",
        AppVersion: CurrentAppVersion,
        DbSchemaVersion: CurrentDbSchemaVersion,
        CreatedAtUtc: DateTime.UtcNow,
        CreatedBy: new BackupCreator(Environment.MachineName, Environment.UserName, Environment.OSVersion.ToString()),
        DataSummary: new BackupDataSummary(0, 0, 0, 0, 0, 0, 0, 0), // detailed counts deferred
        DbSizeBytes: File.Exists(AppPaths.DatabaseFile) ? new FileInfo(AppPaths.DatabaseFile).Length : 0,
        BackupType: backupType,
        Description: description,
        CompanyName: null);
}
