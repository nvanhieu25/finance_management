using System.Linq;
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Services.Implementations;

public class FinanceProjectService : IFinanceProjectService
{
    private readonly IFinanceProjectRepository _repo;

    public FinanceProjectService(IFinanceProjectRepository repo) => _repo = repo;

    public Task<int> CreateAsync(Project project, CancellationToken ct = default) =>
        _repo.AddAsync(project, ct);

    public async Task<ProjectAggregate?> GetAggregateAsync(int projectId, CancellationToken ct = default)
    {
        var project = await _repo.GetWithLinesAsync(projectId, ct);
        if (project is null) return null;

        var inputs = project.Lines
            .OrderBy(l => l.SortOrder)
            .Select(l => l.ToCalcInput())
            .ToList();
        return ProjectCalculator.Aggregate(inputs);
    }

    public async Task<IReadOnlyList<FinanceProjectSummary>> GetAllSummariesAsync(CancellationToken ct = default)
    {
        var projects = await _repo.ListWithLinesAsync(ct);
        return projects
            .Select(p => new FinanceProjectSummary(
                ProjectId: p.Id,
                Code: p.Code,
                Name: p.Name,
                ContractorName: p.ContractorName,
                ContractValue: p.ContractValue,
                Aggregate: ProjectCalculator.Aggregate(
                    p.Lines.OrderBy(l => l.SortOrder).Select(l => l.ToCalcInput()).ToList())))
            .ToList();
    }

    public Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default) =>
        _repo.ListAsync(ct);

    public Task<Project?> GetWithLinesAsync(int id, CancellationToken ct = default) =>
        _repo.GetWithLinesAsync(id, ct);

    public Task UpdateAsync(Project project, CancellationToken ct = default) =>
        _repo.UpdateAsync(project, ct);

    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        _repo.DeleteAsync(id, ct);

    public async Task<int> SaveImportedAsync(IReadOnlyList<Project> projects, CancellationToken ct = default)
    {
        var saved = 0;
        foreach (var project in projects)
        {
            await _repo.AddAsync(project, ct);
            saved++;
        }
        return saved;
    }
}
