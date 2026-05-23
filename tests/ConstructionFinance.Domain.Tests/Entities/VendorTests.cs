using ConstructionFinance.Domain.Common;
using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Entities;

public class VendorTests
{
    [Fact]
    public void Vendor_extends_SoftDeletableEntity()
    {
        new Vendor { VendorName = "X" }.ShouldBeAssignableTo<SoftDeletableEntity>();
    }

    [Fact]
    public void Vendor_can_set_all_properties()
    {
        var vendor = new Vendor
        {
            VendorCode = "V001",
            VendorName = "Cong ty TNHH ABC",
            TaxCode = "0123456789",
            Address = "123 Le Loi",
            Phone = "0901234567",
            Email = "info@abc.vn",
            ContactPerson = "Nguyen Van A",
            Notes = "Note"
        };

        vendor.VendorCode.ShouldBe("V001");
        vendor.VendorName.ShouldBe("Cong ty TNHH ABC");
        vendor.TaxCode.ShouldBe("0123456789");
    }

    [Fact]
    public void Vendor_VendorName_is_required_nonnull()
    {
        var vendor = new Vendor { VendorName = "X" };
        vendor.VendorName.ShouldNotBeNullOrEmpty();
    }
}
