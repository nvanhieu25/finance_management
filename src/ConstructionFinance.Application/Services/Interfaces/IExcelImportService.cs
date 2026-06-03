using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IExcelImportService
{
    /// Parses a .xlsx file and returns parsed summaries + warnings. Does NOT persist.
    Task<ImportResultDto> ParseAsync(string filePath, CancellationToken ct = default);
}
