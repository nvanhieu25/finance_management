using ConstructionFinance.Domain.Common;
namespace ConstructionFinance.Domain.Finance;
public class Dot : BaseEntity
{
    public int PackageId { get; set; }
    public Package Package { get; set; } = null!;
    public int SortOrder { get; set; }
    public DateOnly Date { get; set; }
    public string? Content { get; set; }
    public string? DocumentNo { get; set; }
    public Fund Fund { get; set; }
    public decimal Advance { get; set; }
    public decimal PaymentKLHT { get; set; }
    public decimal RecoveredAdvance { get; set; }
}
