using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Services.Implementations;

public class FinanceProjectService : IFinanceProjectService
{
    private readonly IFinanceProjectRepository _repo;

    public FinanceProjectService(IFinanceProjectRepository repo) => _repo = repo;

    public Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default) =>
        _repo.ListAsync(ct);

    public Task<Project?> GetTreeAsync(int id, CancellationToken ct = default) =>
        _repo.GetTreeAsync(id, ct);

    public Task<IReadOnlyList<Project>> ListTreesAsync(CancellationToken ct = default) =>
        _repo.ListTreesAsync(ct);

    public Task ReplaceTreeAsync(Project project, CancellationToken ct = default) =>
        _repo.ReplaceTreeAsync(project, ct);

    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        _repo.DeleteAsync(id, ct);
}
