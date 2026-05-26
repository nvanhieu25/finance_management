using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface ICostService
{
    Task<IReadOnlyList<CostItemDto>> GetProjectTreeAsync(int projectId, CancellationToken ct = default);
    Task<CostItemDto> CreateNodeAsync(CreateCostItemDto dto, CancellationToken ct = default);
    Task<CostItemDto> UpdateNodeAsync(UpdateCostItemDto dto, CancellationToken ct = default);
    Task RecalculateRollupAsync(int projectId, CancellationToken ct = default);
    Task MoveNodeAsync(int nodeId, int? newParentId, CancellationToken ct = default);
    Task DeleteNodeAsync(int nodeId, CancellationToken ct = default);
}
