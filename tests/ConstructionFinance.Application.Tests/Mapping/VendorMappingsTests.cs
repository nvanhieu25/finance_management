using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Mapping;
using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Mapping;

public class VendorMappingsTests
{
    [Fact]
    public void ToDto_maps_all_fields()
    {
        var v = new Vendor { Id = 7, VendorCode = "V1", VendorName = "ABC", TaxCode = "123", Address = "Addr", Phone = "p", Email = "e@x.vn", ContactPerson = "cp", Notes = "n" };
        var dto = v.ToDto();
        dto.Id.ShouldBe(7);
        dto.VendorName.ShouldBe("ABC");
        dto.TaxCode.ShouldBe("123");
        dto.Email.ShouldBe("e@x.vn");
    }

    [Fact]
    public void CreateDto_ToEntity_maps_fields()
    {
        var dto = new CreateVendorDto(VendorCode: "V1", VendorName: "ABC", TaxCode: "123", Address: null, Phone: null, Email: null, ContactPerson: null, Notes: null);
        var e = dto.ToEntity();
        e.VendorName.ShouldBe("ABC");
        e.VendorCode.ShouldBe("V1");
    }

    [Fact]
    public void UpdateDto_Apply_overwrites_entity_fields()
    {
        var e = new Vendor { Id = 5, VendorName = "Old" };
        var dto = new UpdateVendorDto(Id: 5, VendorCode: "V9", VendorName: "New", TaxCode: null, Address: null, Phone: null, Email: null, ContactPerson: null, Notes: null);
        dto.Apply(e);
        e.VendorName.ShouldBe("New");
        e.VendorCode.ShouldBe("V9");
    }
}
