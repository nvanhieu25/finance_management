using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Vendors;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Vendors;

public class VendorListViewModelTests
{
    private static VendorDto V(int id, string name) =>
        new(id, null, name, null, null, null, null, null, null);

    private static (VendorListViewModel vm, Mock<IVendorService> svc, FakeDialogService dlg) Create(IEnumerable<VendorDto>? initial = null)
    {
        var svc = new Mock<IVendorService>(MockBehavior.Strict);
        svc.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((initial ?? Array.Empty<VendorDto>()).ToList());
        var dlg = new FakeDialogService();
        var vm = new VendorListViewModel(svc.Object, dlg);
        return (vm, svc, dlg);
    }

    [Fact]
    public async Task LoadAsync_populates_Vendors()
    {
        var (vm, _, _) = Create(new[] { V(1, "Alpha"), V(2, "Beta") });
        await vm.LoadAsync();

        vm.Vendors.Count.ShouldBe(2);
        vm.Vendors[0].VendorName.ShouldBe("Alpha");
    }

    [Fact]
    public async Task AddCommand_opens_dialog_and_calls_CreateAsync_on_success()
    {
        var (vm, svc, dlg) = Create();
        svc.Setup(s => s.CreateAsync(It.IsAny<CreateVendorDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(V(99, "New"));
        dlg.DialogResult = true;
        await vm.LoadAsync();

        await vm.AddCommand.ExecuteAsync(null);

        dlg.OpenedDialogs.Count.ShouldBe(1);
        svc.Verify(s => s.CreateAsync(It.IsAny<CreateVendorDto>(), It.IsAny<CancellationToken>()), Times.Once);
        vm.Vendors.ShouldContain(v => v.VendorName == "New");
    }

    [Fact]
    public async Task AddCommand_when_user_cancels_dialog_does_not_call_CreateAsync()
    {
        var (vm, svc, dlg) = Create();
        dlg.DialogResult = false;
        await vm.LoadAsync();

        await vm.AddCommand.ExecuteAsync(null);

        svc.Verify(s => s.CreateAsync(It.IsAny<CreateVendorDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteCommand_with_confirm_calls_DeleteAsync_and_removes_from_list()
    {
        var (vm, svc, dlg) = Create(new[] { V(1, "Alpha") });
        svc.Setup(s => s.DeleteAsync(1, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        dlg.ConfirmResult = true;
        await vm.LoadAsync();
        vm.SelectedVendor = vm.Vendors[0];

        await vm.DeleteCommand.ExecuteAsync(null);

        svc.Verify(s => s.DeleteAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        vm.Vendors.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteCommand_when_user_declines_confirm_does_nothing()
    {
        var (vm, svc, dlg) = Create(new[] { V(1, "Alpha") });
        dlg.ConfirmResult = false;
        await vm.LoadAsync();
        vm.SelectedVendor = vm.Vendors[0];

        await vm.DeleteCommand.ExecuteAsync(null);

        svc.Verify(s => s.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        vm.Vendors.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DeleteCommand_disabled_when_no_selection()
    {
        var (vm, _, _) = Create(new[] { V(1, "Alpha") });
        await vm.LoadAsync();
        vm.SelectedVendor = null;

        vm.DeleteCommand.CanExecute(null).ShouldBeFalse();
    }
}
