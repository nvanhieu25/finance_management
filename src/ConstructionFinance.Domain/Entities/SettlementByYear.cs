using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Entities;

public class SettlementByYear : BaseEntity
{
    public int ProjectSummaryId { get; set; }
    public ProjectSummary? ProjectSummary { get; set; }

    public int Year { get; set; }
    public decimal? KLHT { get; set; }
    public decimal? Advance { get; set; }
    public decimal? NSNN { get; set; }
    public decimal? DTNH { get; set; }
}
