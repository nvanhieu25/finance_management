using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Import;

/// Result of parsing a legacy workbook: one Project (with Lines) per DA sheet, plus warnings.
public record FinanceImportResult(IReadOnlyList<Project> Projects, IReadOnlyList<string> Warnings);
