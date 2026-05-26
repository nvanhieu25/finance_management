using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.ViewModels.Vendors;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Vendors;

public class VendorEditDialogViewModelTests
{
    [Fact]
    public void New_dialog_for_create_has_empty_fields()
    {
        var vm = new VendorEditDialogViewModel(initial: null);

        vm.IsEditMode.ShouldBeFalse();
        vm.DialogTitle.ShouldBe("Thêm nhà cung cấp");
        vm.VendorName.ShouldBeNullOrEmpty();
    }

    [Fact]
    public void Edit_dialog_pre_fills_fields_from_dto()
    {
        var dto = new VendorDto(5, "V1", "Cty ABC", "0123456789", "Hà Nội", "0901", "x@y.vn", "Mr. A", "note");
        var vm = new VendorEditDialogViewModel(initial: dto);

        vm.IsEditMode.ShouldBeTrue();
        vm.DialogTitle.ShouldBe("Sửa nhà cung cấp");
        vm.VendorName.ShouldBe("Cty ABC");
        vm.TaxCode.ShouldBe("0123456789");
        vm.Email.ShouldBe("x@y.vn");
    }

    [Fact]
    public void ToCreateDto_maps_form_fields()
    {
        var vm = new VendorEditDialogViewModel(initial: null)
        {
            VendorCode = "V1",
            VendorName = "Cty",
            TaxCode = "111",
            Email = "e@x.vn"
        };

        var dto = vm.ToCreateDto();

        dto.VendorCode.ShouldBe("V1");
        dto.VendorName.ShouldBe("Cty");
        dto.TaxCode.ShouldBe("111");
        dto.Email.ShouldBe("e@x.vn");
    }

    [Fact]
    public void ToUpdateDto_carries_id_and_form_fields()
    {
        var vm = new VendorEditDialogViewModel(initial: null) { VendorName = "Cty" };

        var dto = vm.ToUpdateDto(42);

        dto.Id.ShouldBe(42);
        dto.VendorName.ShouldBe("Cty");
    }

    [Fact]
    public void HasError_VendorName_true_when_empty()
    {
        var vm = new VendorEditDialogViewModel(initial: null) { VendorName = "" };
        vm.IsValid.ShouldBeFalse();
        vm.VendorNameError.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void IsValid_true_when_VendorName_filled()
    {
        var vm = new VendorEditDialogViewModel(initial: null) { VendorName = "X" };
        vm.IsValid.ShouldBeTrue();
        vm.VendorNameError.ShouldBeNullOrEmpty();
    }
}
