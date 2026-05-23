using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Entities;

[Auditable]
public class Contract : SoftDeletableEntity
{
    public int ApprovalDecisionId { get; set; }
    public int? ContractorVendorId { get; set; }
    public required string ContractCode { get; set; }
    public required string ContractName { get; set; }
    public DateOnly ContractDate { get; set; }
    public DateOnly? SigningDate { get; set; }
    public decimal ContractValue { get; set; }
    public string? Notes { get; set; }

    public ApprovalDecision ApprovalDecision { get; set; } = null!;
    public Vendor? ContractorVendor { get; set; }
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
