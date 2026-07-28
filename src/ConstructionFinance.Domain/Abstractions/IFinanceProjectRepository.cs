using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Domain.Abstractions;

public interface IFinanceProjectRepository
{
    Task<int> AddAsync(Project project, CancellationToken ct = default);
    Task<Project?> GetWithLinesAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListWithLinesAsync(CancellationToken ct = default);
    Task<Project?> GetTreeAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListTreesAsync(CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task UpdateAsync(Project project, CancellationToken ct = default);
}
