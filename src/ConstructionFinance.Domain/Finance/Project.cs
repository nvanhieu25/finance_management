using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Finance;

/// Project-centric flat model (new). Lives in its own namespace during the
/// strangler transition so it does not clash with Entities.Project (legacy).
public class Project : BaseEntity
{
    public string? Code { get; set; }
    public required string Name { get; set; }
    public string? ContractorName { get; set; }
    public decimal ContractValue { get; set; }
    public decimal ContractAnnexValue { get; set; }
    public string? Notes { get; set; }

    public List<ProjectLineItem> Lines { get; set; } = new();
    public List<ProjectDecision> Decisions { get; set; } = new();
}
