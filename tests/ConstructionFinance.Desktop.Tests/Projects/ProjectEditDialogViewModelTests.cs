using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.ViewModels.Projects;
using ConstructionFinance.Domain.Enums;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Projects;

public class ProjectEditDialogViewModelTests
{
    [Fact]
    public void New_dialog_for_contract_has_contractId_set()
    {
        var vm = new ProjectEditDialogViewModel(initial: null, contractId: 7);
        vm.IsEditMode.ShouldBeFalse();
        vm.ContractId.ShouldBe(7);
        vm.DialogTitle.ShouldBe("Thêm Dự án");
    }

    [Fact]
    public void Edit_dialog_pre_fills_from_dto()
    {
        var dto = new ProjectDto(5, 7, "DA-1", "Nhà máy X", "Dân dụng", "Hà Nội",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            ProjectStatus.InProgress, 5_000_000m, 1_000_000m, "Note");
        var vm = new ProjectEditDialogViewModel(initial: dto, contractId: 7);

        vm.IsEditMode.ShouldBeTrue();
        vm.ProjectCode.ShouldBe("DA-1");
        vm.TotalBudget.ShouldBe(5_000_000m);
    }

    [Fact]
    public void ToCreateDto_includes_contractId()
    {
        var vm = new ProjectEditDialogViewModel(initial: null, contractId: 7)
        {
            ProjectCode = "DA-1",
            ProjectName = "X",
            TotalBudget = 1_000_000m
        };
        var dto = vm.ToCreateDto();
        dto.ContractId.ShouldBe(7);
    }

    [Fact]
    public void Validation_required_fields_when_empty()
    {
        var vm = new ProjectEditDialogViewModel(initial: null, contractId: 7);
        vm.IsValid.ShouldBeFalse();
        vm.ProjectCodeError.ShouldNotBeNullOrEmpty();
        vm.ProjectNameError.ShouldNotBeNullOrEmpty();
    }
}
