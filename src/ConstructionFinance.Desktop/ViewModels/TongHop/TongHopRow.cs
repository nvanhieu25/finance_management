using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Desktop.ViewModels.TongHop;

/// Flat row for the Tổng hợp DataGrid (includes dynamic year columns as text).
public class TongHopRow
{
    public int Stt { get; init; }
    public string SheetName { get; init; } = "";
    public string ProjectName { get; init; } = "";
    public string? ContractorName { get; init; }
    public decimal ContractValue { get; init; }
    public decimal Advance { get; init; }
    public decimal PaymentKLHT { get; init; }
    public decimal RecoveredAdvance { get; init; }
    public decimal PaymentToContractor { get; init; }
    public decimal UnrecoveredAdvance { get; init; }
    public decimal DisbursedNSNN { get; init; }
    public decimal DisbursedDTNH { get; init; }
    public decimal TotalDisbursed { get; init; }
    public string YearlySettlementsText { get; init; } = "";
    public ProjectSummaryDto Source { get; init; } = null!;

    public static TongHopRow FromDto(int stt, ProjectSummaryDto dto) => new()
    {
        Stt = stt,
        SheetName = dto.SheetName,
        ProjectName = dto.ProjectName,
        ContractorName = dto.ContractorName,
        ContractValue = dto.ContractValue,
        Advance = dto.Advance,
        PaymentKLHT = dto.PaymentKLHT,
        RecoveredAdvance = dto.RecoveredAdvance,
        PaymentToContractor = dto.PaymentToContractor,
        UnrecoveredAdvance = dto.UnrecoveredAdvance,
        DisbursedNSNN = dto.DisbursedNSNN,
        DisbursedDTNH = dto.DisbursedDTNH,
        TotalDisbursed = dto.TotalDisbursed,
        YearlySettlementsText = FormatYearly(dto.YearlySettlements),
        Source = dto
    };

    private static string FormatYearly(IReadOnlyList<SettlementByYearDto> years) =>
        string.Join(" | ", years.OrderBy(y => y.Year).Select(y =>
        {
            var parts = new List<string>();
            if (y.KLHT is > 0) parts.Add($"KLHT {y.KLHT:N0}");
            if (y.Advance is > 0) parts.Add($"Ứng {y.Advance:N0}");
            if (y.NSNN is > 0) parts.Add($"NSNN {y.NSNN:N0}");
            if (y.DTNH is > 0) parts.Add($"DTNH {y.DTNH:N0}");
            return $"{y.Year}: {string.Join(", ", parts)}";
        }));
}
