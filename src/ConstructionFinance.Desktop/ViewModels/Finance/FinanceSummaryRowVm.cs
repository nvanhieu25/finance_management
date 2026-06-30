using ConstructionFinance.Application.Calc;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// Flat, read-only row for the Tổng hợp DataGrid. Built from a live-computed
/// FinanceProjectSummary; yearly settlements are pre-formatted to a text column.
public class FinanceSummaryRowVm
{
    public int ProjectId { get; init; }
    public string Name { get; init; } = "";
    public string? ContractorName { get; init; }
    public decimal ContractValue { get; init; }
    public decimal SumAdvance { get; init; }
    public decimal SumRecovered { get; init; }
    public decimal UnrecoveredAdvance { get; init; }
    public decimal SumNSNN { get; init; }
    public decimal SumNgoaiHoi { get; init; }
    public decimal TotalDisbursed { get; init; }
    public string YearlySettlementsText { get; init; } = "";

    public static FinanceSummaryRowVm FromSummary(FinanceProjectSummary s) => new()
    {
        ProjectId = s.ProjectId,
        Name = s.Name,
        ContractorName = s.ContractorName,
        ContractValue = s.ContractValue,
        SumAdvance = s.Aggregate.SumAdvance,
        SumRecovered = s.Aggregate.SumRecovered,
        UnrecoveredAdvance = s.Aggregate.UnrecoveredAdvance,
        SumNSNN = s.Aggregate.SumNSNN,
        SumNgoaiHoi = s.Aggregate.SumNgoaiHoi,
        TotalDisbursed = s.Aggregate.TotalDisbursed,
        YearlySettlementsText = FormatYearly(s.Aggregate.YearlySettlements)
    };

    public static string FormatYearly(IReadOnlyList<YearSettlement> years) =>
        string.Join(" | ", years.OrderBy(y => y.Year).Select(y =>
        {
            var parts = new List<string>();
            if (y.KLHT > 0) parts.Add($"KLHT {y.KLHT:#,##0}");
            if (y.Advance > 0) parts.Add($"Ứng {y.Advance:#,##0}");
            if (y.NSNN > 0) parts.Add($"NSNN {y.NSNN:#,##0}");
            if (y.NgoaiHoi > 0) parts.Add($"NgoaiHoi {y.NgoaiHoi:#,##0}");
            return $"{y.Year}: {string.Join(", ", parts)}";
        }));
}
