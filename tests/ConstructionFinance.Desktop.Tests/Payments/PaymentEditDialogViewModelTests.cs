using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.ViewModels.Payments;
using ConstructionFinance.Domain.Enums;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Payments;

public class PaymentEditDialogViewModelTests
{
    [Fact]
    public void New_dialog_defaults_today_and_carries_costItemId()
    {
        var vm = new PaymentEditDialogViewModel(initial: null, costItemId: 7, costItemEstimatedAmount: 1_000m);

        vm.IsEditMode.ShouldBeFalse();
        vm.CostItemId.ShouldBe(7);
        vm.PaymentDate.ShouldBe(DateOnly.FromDateTime(DateTime.Today));
    }

    [Fact]
    public void Edit_dialog_pre_fills_from_dto()
    {
        var dto = new PaymentDto(5, 7, 2, PaymentType.Installment, new DateOnly(2026, 5, 15), 300m, 30m, "HD-001", new DateOnly(2026, 5, 14), "Desc", "Notes");
        var vm = new PaymentEditDialogViewModel(initial: dto, costItemId: 7, costItemEstimatedAmount: 1_000m);

        vm.IsEditMode.ShouldBeTrue();
        vm.PaymentType.ShouldBe(PaymentType.Installment);
        vm.Amount.ShouldBe(300m);
        vm.InvoiceNo.ShouldBe("HD-001");
    }

    [Fact]
    public void ToCreateDto_carries_form_fields()
    {
        var vm = new PaymentEditDialogViewModel(initial: null, costItemId: 7, costItemEstimatedAmount: 1_000m)
        {
            PaymentType = PaymentType.Advance,
            Amount = 200m
        };
        var dto = vm.ToCreateDto();
        dto.CostItemId.ShouldBe(7);
        dto.PaymentType.ShouldBe(PaymentType.Advance);
        dto.Amount.ShouldBe(200m);
    }

    [Fact]
    public void ToUpdateDto_carries_id_and_form_fields()
    {
        var dto = new PaymentDto(5, 7, 2, PaymentType.Installment, new DateOnly(2026, 5, 15), 300m, 30m, "HD-001", null, null, null);
        var vm = new PaymentEditDialogViewModel(initial: dto, costItemId: 7, costItemEstimatedAmount: 1_000m)
        {
            Amount = 400m
        };
        var update = vm.ToUpdateDto(5);
        update.Id.ShouldBe(5);
        update.Amount.ShouldBe(400m);
    }

    [Fact]
    public void Validation_Amount_must_be_positive()
    {
        var vm = new PaymentEditDialogViewModel(initial: null, costItemId: 7, costItemEstimatedAmount: 1_000m) { Amount = 0m };
        vm.AmountError.ShouldNotBeNullOrEmpty();
        vm.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validation_Amount_positive_is_valid()
    {
        var vm = new PaymentEditDialogViewModel(initial: null, costItemId: 7, costItemEstimatedAmount: 1_000m) { Amount = 100m };
        vm.AmountError.ShouldBeNull();
        vm.IsValid.ShouldBeTrue();
    }
}
