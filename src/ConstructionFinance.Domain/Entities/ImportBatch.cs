using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Entities;

public class ImportBatch : BaseEntity
{
    public required string SourceFileName { get; set; }
    public string? SourceFileHash { get; set; }
    public DateTime ImportedAtUtc { get; set; }
    public int SheetsFound { get; set; }
    public int ProjectsImported { get; set; }
    public string? Description { get; set; }
    public string? WarningsJson { get; set; }

    public List<ProjectSummary> Summaries { get; set; } = new();
}
