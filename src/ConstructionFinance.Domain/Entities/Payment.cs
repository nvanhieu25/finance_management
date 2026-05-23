using ConstructionFinance.Domain.Common;
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Domain.Entities;

[Auditable]
public class Payment : BaseEntity
{
    public int CostItemId { get; set; }
    public int PaymentNo { get; set; }
    public PaymentType PaymentType { get; set; }
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public decimal Percentage { get; set; }
    public string? InvoiceNo { get; set; }
    public DateOnly? InvoiceDate { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }

    public CostItem CostItem { get; set; } = null!;
}
