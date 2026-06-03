using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IProjectSummaryService
{
    Task<int> SaveBatchAsync(ImportResultDto parsed, CancellationToken ct = default);
    Task<IReadOnlyList<ImportBatchDto>> ListBatchesAsync(CancellationToken ct = default);
    Task<ImportBatchDto?> GetLatestBatchAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ProjectSummaryDto>> ListByBatchAsync(int batchId, CancellationToken ct = default);
    Task DeleteBatchAsync(int batchId, CancellationToken ct = default);
    Task<TongHopAggregateDto> AggregateAsync(int batchId, CancellationToken ct = default);
}
