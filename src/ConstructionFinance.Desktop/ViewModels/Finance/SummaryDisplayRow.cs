using ConstructionFinance.Application.Calc;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// Read-only, pre-formatted row shared by the per-project Tổng hợp tab and the
/// all-projects Tổng hợp screen. Money is formatted with MoneyText ("." grouping,
/// 0 → blank); Project/GrandTotal rows are bold. String facade — never bound TwoWay.
public class SummaryDisplayRow
{
    public int ProjectId { get; init; }
    public string Ordinal { get; init; } = "";
    public string Label { get; init; } = "";
    public string Contractor { get; init; } = "";
    public string ContractValue { get; init; } = "";
    public string Advance { get; init; } = "";
    public string KLHT { get; init; } = "";
    public string Recovered { get; init; } = "";
    public string DisbursedToContractor { get; init; } = "";
    public string Unrecovered { get; init; } = "";
    public string NSNN { get; init; } = "";
    public string NgoaiHoi { get; init; } = "";
    public string TotalDisbursed { get; init; } = "";
    public string YearlyText { get; init; } = "";
    public bool IsBold { get; init; }

    public static SummaryDisplayRow From(WorkbookSummaryRow row)
    {
        var a = row.Aggregate;
        return new SummaryDisplayRow
        {
            ProjectId = row.ProjectId,
            Ordinal = row.Ordinal,
            Label = row.Label,
            Contractor = row.Contractor ?? "",
            ContractValue = MoneyText.Format(row.ContractValue),
            Advance = MoneyText.Format(a.SumAdvance),
            KLHT = MoneyText.Format(a.SumKLHT),
            Recovered = MoneyText.Format(a.SumRecovered),
            DisbursedToContractor = MoneyText.Format(a.TotalDisbursed),
            Unrecovered = MoneyText.Format(a.UnrecoveredAdvance),
            NSNN = MoneyText.Format(a.DisbursedNSNN),
            NgoaiHoi = MoneyText.Format(a.DisbursedNgoaiHoi),
            TotalDisbursed = MoneyText.Format(a.TotalDisbursed),
            YearlyText = FormatYearly(a.Yearly),
            IsBold = row.Kind != SummaryRowKind.ViTri
        };
    }

    public static string FormatYearly(IReadOnlyList<FiscalYearSettlement> years) =>
        string.Join("  |  ", years
            .Where(y => y.NSNN != 0m || y.NgoaiHoi != 0m)
            .OrderBy(y => y.FiscalYear)
            .Select(y =>
            {
                var parts = new List<string>();
                if (y.NSNN != 0m) parts.Add($"NSNN {MoneyText.Format(y.NSNN)}");
                if (y.NgoaiHoi != 0m) parts.Add($"DTNH {MoneyText.Format(y.NgoaiHoi)}");
                return $"{y.FiscalYear}: {string.Join(", ", parts)}";
            }));
}
