using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Entities;

[Auditable]
public class Vendor : SoftDeletableEntity
{
    public string? VendorCode { get; set; }
    public required string VendorName { get; set; }
    public string? TaxCode { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? ContactPerson { get; set; }
    public string? Notes { get; set; }
}
