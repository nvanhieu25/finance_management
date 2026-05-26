using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IDecisionService
{
    Task<DecisionDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<DecisionDto>> GetAllAsync(CancellationToken ct = default);
    Task<DecisionDto> CreateAsync(CreateDecisionDto dto, CancellationToken ct = default);
    Task<DecisionDto> UpdateAsync(UpdateDecisionDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
