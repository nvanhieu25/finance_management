using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Settings;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Settings;

public class PreferencesViewModelTests
{
    private static (PreferencesViewModel vm, Mock<IAppSettingService> svc, FakeDialogService dlg) Create(string? autoBackupValue = null)
    {
        var svc = new Mock<IAppSettingService>(MockBehavior.Strict);
        svc.Setup(s => s.GetAsync("auto_backup_on_exit", It.IsAny<CancellationToken>())).ReturnsAsync(autoBackupValue);
        return (new PreferencesViewModel(svc.Object, new FakeDialogService()), svc, new FakeDialogService());
    }

    [Fact]
    public async Task LoadAsync_AutoBackupOnExit_true_when_setting_is_true()
    {
        var (vm, _, _) = Create("true");
        await vm.LoadAsync();
        vm.AutoBackupOnExit.ShouldBeTrue();
    }

    [Fact]
    public async Task LoadAsync_defaults_to_true_when_setting_missing()
    {
        var (vm, _, _) = Create(autoBackupValue: null);
        await vm.LoadAsync();
        vm.AutoBackupOnExit.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAsync_persists_AutoBackupOnExit()
    {
        var (vm, svc, _) = Create();
        svc.Setup(s => s.SetAsync("auto_backup_on_exit", "false", null, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await vm.LoadAsync();
        vm.AutoBackupOnExit = false;

        await vm.SaveAsync();

        svc.Verify(s => s.SetAsync("auto_backup_on_exit", "false", null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
