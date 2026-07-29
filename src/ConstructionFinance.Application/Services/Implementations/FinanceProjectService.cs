using System.Linq;
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Import;
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

    public async Task<TreeAggregate?> GetTreeAggregateAsync(int projectId, CancellationToken ct = default)
    {
        var project = await _repo.GetTreeAsync(projectId, ct);
        return project is null ? null : TreeCalculator.AggregateTree(project);
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

    public Task ReplaceTreeAsync(Project project, CancellationToken ct = default) =>
        _repo.ReplaceTreeAsync(project, ct);

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

    /// Apply the user's per-project import decisions. Skip writes nothing;
    /// Overwrite replaces the existing project's lines wholesale; AddNew inserts
    /// under a free name so an import can never silently duplicate a project.
    public async Task<int> SaveMergedAsync(IReadOnlyList<ImportCandidate> candidates, CancellationToken ct = default)
    {
        var existing = await _repo.ListAsync(ct);
        var taken = new HashSet<string>(existing.Select(p => p.Name.Trim()), StringComparer.OrdinalIgnoreCase);

        var written = 0;
        foreach (var candidate in candidates)
        {
            switch (candidate.Resolution)
            {
                case ImportResolution.Skip:
                    continue;

                case ImportResolution.Overwrite when candidate.ExistingId is { } id:
                    candidate.Parsed.Id = id;
                    // Normalize so re-importing an Excel cell like "  da1 " does not
                    // rewrite the stored "DA1" with the padded spelling.
                    candidate.Parsed.Name = candidate.Parsed.Name.Trim();
                    await _repo.UpdateAsync(candidate.Parsed, ct);
                    written++;
                    break;

                default:
                    // NextFreeName already trims, so the persisted name is normalized.
                    var name = ImportMerge.NextFreeName(candidate.Parsed.Name, taken);
                    candidate.Parsed.Name = name;
                    candidate.Parsed.Id = 0;
                    taken.Add(name);
                    await _repo.AddAsync(candidate.Parsed, ct);
                    written++;
                    break;
            }
        }
        return written;
    }
}
