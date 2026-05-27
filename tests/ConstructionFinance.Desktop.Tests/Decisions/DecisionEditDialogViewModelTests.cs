using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.ViewModels.Decisions;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Decisions;

public class DecisionEditDialogViewModelTests
{
    [Fact]
    public void New_dialog_has_empty_fields()
    {
        var vm = new DecisionEditDialogViewModel(initial: null);
        vm.IsEditMode.ShouldBeFalse();
        vm.DialogTitle.ShouldBe("Thêm Quyết định phê duyệt");
        vm.DecisionCode.ShouldBeNullOrEmpty();
    }

    [Fact]
    public void Edit_dialog_pre_fills_from_dto()
    {
        var dto = new DecisionDto(5, "1234/QD", "Tên QĐ", new DateOnly(2026, 3, 15), "UBND Tỉnh ABC", 50_000_000_000m, "Note");
        var vm = new DecisionEditDialogViewModel(initial: dto);

        vm.IsEditMode.ShouldBeTrue();
        vm.DialogTitle.ShouldBe("Sửa Quyết định phê duyệt");
        vm.DecisionCode.ShouldBe("1234/QD");
        vm.IssuingAuthority.ShouldBe("UBND Tỉnh ABC");
        vm.TotalApprovedAmount.ShouldBe(50_000_000_000m);
    }

    [Fact]
    public void ToCreateDto_maps_all_fields()
    {
        var vm = new DecisionEditDialogViewModel(initial: null)
        {
            DecisionCode = "1234/QD",
            DecisionTitle = "Phê duyệt",
            DecisionDate = new DateOnly(2026, 3, 15),
            IssuingAuthority = "UBND",
            TotalApprovedAmount = 100_000m
        };

        var dto = vm.ToCreateDto();
        dto.DecisionCode.ShouldBe("1234/QD");
        dto.TotalApprovedAmount.ShouldBe(100_000m);
    }

    [Fact]
    public void Validation_DecisionCode_required()
    {
        var vm = new DecisionEditDialogViewModel(initial: null) { DecisionCode = "" };
        vm.DecisionCodeError.ShouldNotBeNullOrEmpty();
        vm.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validation_TotalApprovedAmount_negative_invalid()
    {
        var vm = new DecisionEditDialogViewModel(initial: null)
        {
            DecisionCode = "X",
            DecisionTitle = "Y",
            IssuingAuthority = "Z",
            TotalApprovedAmount = -1
        };
        vm.TotalApprovedAmountError.ShouldNotBeNullOrEmpty();
        vm.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void IsValid_true_when_all_required_filled()
    {
        var vm = new DecisionEditDialogViewModel(initial: null)
        {
            DecisionCode = "X",
            DecisionTitle = "Y",
            IssuingAuthority = "Z",
            TotalApprovedAmount = 100m
        };
        vm.IsValid.ShouldBeTrue();
    }
}
