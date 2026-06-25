using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Domain.Abstractions;

public interface IProjectSummaryRepository
{
    Task<ImportBatch?> GetBatchByIdAsync(int batchId, CancellationToken ct = default);
    Task<IReadOnlyList<ImportBatch>> ListBatchesAsync(CancellationToken ct = default);
    Task<ImportBatch?> GetLatestBatchAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ImportBatch>> FindByHashAsync(string sourceFileHash, CancellationToken ct = default);
    Task<ImportBatch?> FindLatestByFileNameAsync(string sourceFileName, CancellationToken ct = default);
    Task AddBatchAsync(ImportBatch batch, CancellationToken ct = default);
    Task DeleteBatchAsync(int batchId, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectSummary>> ListByBatchAsync(int batchId, CancellationToken ct = default);
}
