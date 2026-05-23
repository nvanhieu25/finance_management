using ConstructionFinance.Domain.Common;
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Domain.Entities;

[Auditable]
public class Project : SoftDeletableEntity
{
    public int ContractId { get; set; }
    public required string ProjectCode { get; set; }
    public required string ProjectName { get; set; }
    public string? ProjectType { get; set; }
    public string? Location { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Planning;
    public decimal TotalBudget { get; set; }
    public decimal TotalActual { get; set; }
    public string? Notes { get; set; }

    public Contract Contract { get; set; } = null!;
    public ICollection<CostItem> CostItems { get; set; } = new List<CostItem>();
}
