using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Application.Mapping;

public static class VendorMappings
{
    public static VendorDto ToDto(this Vendor e) =>
        new(e.Id, e.VendorCode, e.VendorName, e.TaxCode, e.Address, e.Phone, e.Email, e.ContactPerson, e.Notes);

    public static Vendor ToEntity(this CreateVendorDto d) =>
        new()
        {
            VendorCode = d.VendorCode,
            VendorName = d.VendorName,
            TaxCode = d.TaxCode,
            Address = d.Address,
            Phone = d.Phone,
            Email = d.Email,
            ContactPerson = d.ContactPerson,
            Notes = d.Notes
        };

    public static void Apply(this UpdateVendorDto d, Vendor e)
    {
        e.VendorCode = d.VendorCode;
        e.VendorName = d.VendorName;
        e.TaxCode = d.TaxCode;
        e.Address = d.Address;
        e.Phone = d.Phone;
        e.Email = d.Email;
        e.ContactPerson = d.ContactPerson;
        e.Notes = d.Notes;
    }
}
