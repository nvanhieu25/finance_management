using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Import;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IFinanceProjectService
{
    Task<int> CreateAsync(Project project, CancellationToken ct = default);
    Task<ProjectAggregate?> GetAggregateAsync(int projectId, CancellationToken ct = default);
    Task<TreeAggregate?> GetTreeAggregateAsync(int projectId, CancellationToken ct = default);
    Task<IReadOnlyList<FinanceProjectSummary>> GetAllSummariesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default);
    Task<Project?> GetWithLinesAsync(int id, CancellationToken ct = default);
    Task UpdateAsync(Project project, CancellationToken ct = default);
    Task ReplaceTreeAsync(Project project, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<int> SaveImportedAsync(IReadOnlyList<Project> projects, CancellationToken ct = default);
    Task<int> SaveMergedAsync(IReadOnlyList<ImportCandidate> candidates, CancellationToken ct = default);
}
