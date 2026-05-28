using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Payments;
using ConstructionFinance.Domain.Enums;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Payments;

public class PaymentListDialogViewModelTests
{
    private static PaymentDto P(int id, int costItemId, int no, decimal amount) =>
        new(id, costItemId, no, PaymentType.Installment, new DateOnly(2026, 5, 1), amount, 10m, null, null, null, null);

    private static (PaymentListDialogViewModel vm, Mock<IPaymentService> svc, FakeDialogService dlg)
        Create(int costItemId = 7, IEnumerable<PaymentDto>? initial = null, PaymentSummaryDto? summary = null)
    {
        var svc = new Mock<IPaymentService>(MockBehavior.Strict);
        svc.Setup(s => s.GetByCostItemAsync(costItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((initial ?? Array.Empty<PaymentDto>()).ToList());
        svc.Setup(s => s.GetSummaryAsync(costItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary ?? new PaymentSummaryDto(costItemId, 1_000m, 0m, 1_000m, 0m));
        var dlg = new FakeDialogService();
        var vm = new PaymentListDialogViewModel(svc.Object, dlg);
        vm.SetCostItem(costItemId, "Móng cọc");
        return (vm, svc, dlg);
    }

    [Fact]
    public async Task LoadAsync_populates_Payments_and_Summary()
    {
        var (vm, _, _) = Create(initial: new[] { P(1, 7, 1, 300m) }, summary: new PaymentSummaryDto(7, 1_000m, 300m, 700m, 30m));
        await vm.LoadAsync();

        vm.Payments.Count.ShouldBe(1);
        vm.Summary.TotalPaid.ShouldBe(300m);
        vm.Summary.CompletionPct.ShouldBe(30m);
    }

    [Fact]
    public async Task DialogTitle_includes_cost_item_name()
    {
        var (vm, _, _) = Create();
        await vm.LoadAsync();
        vm.DialogTitle.ShouldContain("Móng cọc");
    }

    [Fact]
    public async Task AddCommand_opens_dialog_and_calls_AddAsync_on_confirm()
    {
        var (vm, svc, dlg) = Create();
        svc.Setup(s => s.AddAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(P(99, 7, 1, 500m));
        dlg.DialogResult = true;
        await vm.LoadAsync();

        await vm.AddCommand.ExecuteAsync(null);

        svc.Verify(s => s.AddAsync(It.Is<CreatePaymentDto>(d => d.CostItemId == 7), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddCommand_does_nothing_when_dialog_cancelled()
    {
        var (vm, svc, dlg) = Create();
        dlg.DialogResult = false;
        await vm.LoadAsync();

        await vm.AddCommand.ExecuteAsync(null);

        svc.Verify(s => s.AddAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteCommand_calls_DeleteAsync_when_confirmed()
    {
        var (vm, svc, dlg) = Create(initial: new[] { P(1, 7, 1, 300m) });
        svc.Setup(s => s.DeleteAsync(1, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        dlg.ConfirmResult = true;
        await vm.LoadAsync();
        vm.SelectedPayment = vm.Payments[0];

        await vm.DeleteCommand.ExecuteAsync(null);

        svc.Verify(s => s.DeleteAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCommand_skipped_when_user_declines()
    {
        var (vm, svc, dlg) = Create(initial: new[] { P(1, 7, 1, 300m) });
        dlg.ConfirmResult = false;
        await vm.LoadAsync();
        vm.SelectedPayment = vm.Payments[0];

        await vm.DeleteCommand.ExecuteAsync(null);

        svc.Verify(s => s.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void DeleteCommand_disabled_when_no_selection()
    {
        var (vm, _, _) = Create();
        vm.SelectedPayment = null;
        vm.DeleteCommand.CanExecute(null).ShouldBeFalse();
        vm.EditCommand.CanExecute(null).ShouldBeFalse();
    }
}
