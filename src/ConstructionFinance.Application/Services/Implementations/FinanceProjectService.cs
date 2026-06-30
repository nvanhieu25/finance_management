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
}
