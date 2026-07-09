using ConstructionFinance.Application.Import;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IFinanceImportService
{
    /// Parse a legacy .xlsx into finance Projects (raw cells only). Does NOT persist.
    Task<FinanceImportResult> ParseWorkbookAsync(string filePath, CancellationToken ct = default);
}
