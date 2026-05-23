using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Data.Repositories;

public interface ICostRepository : IRepository<CostItem>
{
    Task<CostItem> AddNodeAsync(int projectId, int? parentId, CostItem newNode, CancellationToken ct = default);
    Task<IReadOnlyList<CostItem>> GetTreeByProjectAsync(int projectId, CancellationToken ct = default);
    Task<IReadOnlyList<CostItem>> GetSubtreeAsync(int nodeId, CancellationToken ct = default);
}
