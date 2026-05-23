using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Entities;

[Auditable]
public class CostItem : SoftDeletableEntity
{
    public int ProjectId { get; set; }
    public int? ParentId { get; set; }
    public int? UnitTypeId { get; set; }
    public int? VendorId { get; set; }

    public string? ItemCode { get; set; }
    public required string ItemName { get; set; }
    public int Level { get; set; }
    public required string Path { get; set; }
    public int SortOrder { get; set; }
    public bool IsLeaf { get; set; } = true;

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal EstimatedAmount { get; set; }

    public string? BidPackage { get; set; }
    public string? Notes { get; set; }

    public Project Project { get; set; } = null!;
    public CostItem? Parent { get; set; }
    public ICollection<CostItem> Children { get; set; } = new List<CostItem>();
    public CostUnitType? UnitType { get; set; }
    public Vendor? Vendor { get; set; }
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
