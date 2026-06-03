using System.Text.Json;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Application.Mapping;

public static class ProjectSummaryMappings
{
    public static ProjectSummaryDto ToDto(this ProjectSummary e) => new(
        e.SheetName,
        e.ProjectName,
        e.ContractorName,
        e.ContractValue,
        e.Advance,
        e.PaymentKLHT,
        e.RecoveredAdvance,
        e.PaymentToContractor,
        e.UnrecoveredAdvance,
        e.DisbursedNSNN,
        e.DisbursedDTNH,
        e.TotalDisbursed,
        e.YearlySettlements.Select(y => y.ToDto()).ToList());

    public static SettlementByYearDto ToDto(this SettlementByYear e) =>
        new(e.Year, e.KLHT, e.Advance, e.NSNN, e.DTNH);

    public static ImportBatchDto ToDto(this ImportBatch b)
    {
        IReadOnlyList<string> warnings = string.IsNullOrEmpty(b.WarningsJson)
            ? Array.Empty<string>()
            : JsonSerializer.Deserialize<List<string>>(b.WarningsJson) ?? new List<string>();
        return new ImportBatchDto(
            b.Id,
            b.SourceFileName,
            b.ImportedAtUtc,
            b.SheetsFound,
            b.ProjectsImported,
            b.Description,
            warnings);
    }

    public static ProjectSummary ToEntity(this ProjectSummaryDto dto) => new()
    {
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
        YearlySettlements = dto.YearlySettlements.Select(y => y.ToEntity()).ToList()
    };

    public static SettlementByYear ToEntity(this SettlementByYearDto dto) => new()
    {
        Year = dto.Year,
        KLHT = dto.KLHT,
        Advance = dto.Advance,
        NSNN = dto.NSNN,
        DTNH = dto.DTNH
    };
}
