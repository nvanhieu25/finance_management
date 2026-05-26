using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IContractService
{
    Task<ContractDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ContractDto>> GetByDecisionAsync(int decisionId, CancellationToken ct = default);
    Task<ContractDto> CreateAsync(CreateContractDto dto, CancellationToken ct = default);
    Task<ContractDto> UpdateAsync(UpdateContractDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
