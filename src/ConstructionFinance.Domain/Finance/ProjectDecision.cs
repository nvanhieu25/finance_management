using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Domain.Finance;

/// Many-to-many link between a finance Project and a legacy ApprovalDecision.
public class ProjectDecision
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public int ApprovalDecisionId { get; set; }
    public ApprovalDecision Decision { get; set; } = null!;
}
