using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Entities;

[Auditable]
public class ApprovalDecision : SoftDeletableEntity
{
    public required string DecisionCode { get; set; }
    public required string DecisionTitle { get; set; }
    public DateOnly DecisionDate { get; set; }
    public required string IssuingAuthority { get; set; }
    public decimal TotalApprovedAmount { get; set; }
    public string? Notes { get; set; }

    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}
