using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IFinanceExportService
{
    /// Write a workbook (one DA sheet per project + a TH summary) with all figures
    /// computed by ProjectCalculator, as static values.
    Task ExportAsync(IReadOnlyList<Project> projects, string targetPath, CancellationToken ct = default);
}
