using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Settings;
using ConstructionFinance.Infrastructure.Backup;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Settings;

public class BackupManagementViewModelTests
{
    private static (BackupManagementViewModel vm, Mock<IBackupService> bsvc, Mock<IRestoreService> rsvc, FakeDialogService dlg) Create(IEnumerable<BackupFileInfo>? listing = null)
    {
        var bsvc = new Mock<IBackupService>(MockBehavior.Strict);
        var rsvc = new Mock<IRestoreService>(MockBehavior.Strict);
        bsvc.Setup(s => s.ListBackups(It.IsAny<string>()))
            .Returns((listing ?? Array.Empty<BackupFileInfo>()).ToList());
        var dlg = new FakeDialogService();
        var vm = new BackupManagementViewModel(bsvc.Object, rsvc.Object, dlg);
        return (vm, bsvc, rsvc, dlg);
    }

    private static BackupFileInfo Info(string name, string type = "manual") =>
        new(Path.Combine("/tmp", name), name, DateTime.UtcNow, 12345, type, null);

    [Fact]
    public async Task LoadAsync_populates_list()
    {
        var (vm, _, _, _) = Create(new[] { Info("backup_20260520_120000_manual.cfb"), Info("backup_20260519_180000_on_exit.cfb", "on_exit") });
        await vm.LoadAsync();

        vm.Backups.Count.ShouldBe(2);
    }

    [Fact]
    public async Task BackupNowCommand_calls_CreateBackupAsync()
    {
        var (vm, bsvc, _, dlg) = Create();
        bsvc.Setup(s => s.CreateBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BackupManifest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Info("backup_new.cfb"));
        await vm.LoadAsync();

        await vm.BackupNowCommand.ExecuteAsync(null);

        bsvc.Verify(s => s.CreateBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.Is<BackupManifest>(m => m.BackupType == "manual"), It.IsAny<CancellationToken>()), Times.Once);
        dlg.ShownMessages.ShouldContain(m => m.Contains("backup") || m.Contains("Đã backup"));
    }

    [Fact]
    public async Task RestoreSelectedCommand_blocked_when_user_declines_first_confirm()
    {
        var (vm, _, rsvc, dlg) = Create(new[] { Info("b.cfb") });
        dlg.ConfirmResult = false;
        await vm.LoadAsync();
        vm.SelectedBackup = vm.Backups[0];

        await vm.RestoreSelectedCommand.ExecuteAsync(null);

        rsvc.Verify(s => s.RestoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
