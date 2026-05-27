using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.ViewModels.Contracts;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Contracts;

public class ContractEditDialogViewModelTests
{
    [Fact]
    public void New_dialog_carries_decision_id()
    {
        var vm = new ContractEditDialogViewModel(initial: null, decisionId: 7);
        vm.IsEditMode.ShouldBeFalse();
        vm.DecisionId.ShouldBe(7);
        vm.DialogTitle.ShouldBe("Thêm Hợp đồng");
    }

    [Fact]
    public void Edit_dialog_pre_fills_from_dto()
    {
        var dto = new ContractDto(5, 7, null, "HD-001", "Tên HĐ", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 2), 1_000_000m, "Note");
        var vm = new ContractEditDialogViewModel(initial: dto, decisionId: 7);

        vm.IsEditMode.ShouldBeTrue();
        vm.DialogTitle.ShouldBe("Sửa Hợp đồng");
        vm.ContractCode.ShouldBe("HD-001");
        vm.ContractValue.ShouldBe(1_000_000m);
    }

    [Fact]
    public void ToCreateDto_includes_decisionId()
    {
        var vm = new ContractEditDialogViewModel(initial: null, decisionId: 7)
        {
            ContractCode = "HD-1",
            ContractName = "X",
            ContractDate = new DateOnly(2026, 5, 1),
            ContractValue = 1_000_000m
        };

        var dto = vm.ToCreateDto();
        dto.ApprovalDecisionId.ShouldBe(7);
        dto.ContractCode.ShouldBe("HD-1");
    }

    [Fact]
    public void Validation_required_fields()
    {
        var vm = new ContractEditDialogViewModel(initial: null, decisionId: 7);
        vm.IsValid.ShouldBeFalse();
        vm.ContractCodeError.ShouldNotBeNullOrEmpty();
        vm.ContractNameError.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void ContractValue_negative_invalid()
    {
        var vm = new ContractEditDialogViewModel(initial: null, decisionId: 7)
        {
            ContractCode = "X", ContractName = "Y", ContractValue = -1
        };
        vm.ContractValueError.ShouldNotBeNullOrEmpty();
    }
}
