using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Finance;

/// One đợt (line item). Stores only raw inputs; derived figures are computed
/// live by ProjectCalculator.
public class ProjectLineItem : BaseEntity
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public int SortOrder { get; set; }
    public DateOnly Date { get; set; }
    public string? PackageName { get; set; }
    public string? ContractRef { get; set; }

    public decimal Advance { get; set; }
    public decimal PaymentKLHT { get; set; }
    public decimal RecoveredAdvance { get; set; }
    public decimal DisbursedNSNN { get; set; }
    public decimal DisbursedNgoaiHoi { get; set; }
}
