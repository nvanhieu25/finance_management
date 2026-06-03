using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Entities;

public class ProjectSummary : BaseEntity
{
    public int ImportBatchId { get; set; }
    public ImportBatch? Batch { get; set; }

    public required string SheetName { get; set; }
    public required string ProjectName { get; set; }
    public string? ContractorName { get; set; }

    public decimal ContractValue { get; set; }
    public decimal Advance { get; set; }
    public decimal PaymentKLHT { get; set; }
    public decimal RecoveredAdvance { get; set; }
    public decimal PaymentToContractor { get; set; }
    public decimal UnrecoveredAdvance { get; set; }
    public decimal DisbursedNSNN { get; set; }
    public decimal DisbursedDTNH { get; set; }
    public decimal TotalDisbursed { get; set; }

    public List<SettlementByYear> YearlySettlements { get; set; } = new();
}
