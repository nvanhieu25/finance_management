using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IExcelExportService
{
    /// Opens the template file, overwrites the "Tổng hợp" sheet data rows, saves to targetPath.
    Task<string> ExportTongHopAsync(
        string templatePath,
        string targetPath,
        IReadOnlyList<ProjectSummaryDto> summaries,
        CancellationToken ct = default);
}
