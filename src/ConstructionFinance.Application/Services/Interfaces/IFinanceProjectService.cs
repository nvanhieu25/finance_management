using ConstructionFinance.Application.Calc;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IFinanceProjectService
{
    Task<int> CreateAsync(Project project, CancellationToken ct = default);
    Task<ProjectAggregate?> GetAggregateAsync(int projectId, CancellationToken ct = default);
}
