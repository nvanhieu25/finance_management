namespace ConstructionFinance.Application.DTOs;

public record ProjectSummaryDto(
    string SheetName,
    string ProjectName,
    string? ContractorName,
    decimal ContractValue,
    decimal Advance,
    decimal PaymentKLHT,
    decimal RecoveredAdvance,
    decimal PaymentToContractor,
    decimal UnrecoveredAdvance,
    decimal DisbursedNSNN,
    decimal DisbursedDTNH,
    decimal TotalDisbursed,
    IReadOnlyList<SettlementByYearDto> YearlySettlements);

public record SettlementByYearDto(int Year, decimal? KLHT, decimal? Advance, decimal? NSNN, decimal? DTNH);

public record ImportResultDto(
    string SourceFileName,
    string SourceFileHash,
    int SheetsFound,
    IReadOnlyList<ProjectSummaryDto> Summaries,
    IReadOnlyList<string> Warnings);

public record ImportBatchDto(
    int Id,
    string SourceFileName,
    DateTime ImportedAtUtc,
    int SheetsFound,
    int ProjectsImported,
    string? Description,
    IReadOnlyList<string> Warnings);

public record TongHopAggregateDto(
    int BatchId,
    int ProjectCount,
    decimal ContractValue,
    decimal Advance,
    decimal PaymentKLHT,
    decimal RecoveredAdvance,
    decimal PaymentToContractor,
    decimal UnrecoveredAdvance,
    decimal DisbursedNSNN,
    decimal DisbursedDTNH,
    decimal TotalDisbursed,
    IReadOnlyList<YearAggregateDto> ByYear);

public record YearAggregateDto(int Year, decimal? KLHT, decimal? Advance, decimal? NSNN, decimal? DTNH);
