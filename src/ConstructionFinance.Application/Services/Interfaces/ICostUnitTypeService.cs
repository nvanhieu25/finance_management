using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface ICostUnitTypeService
{
    Task<IReadOnlyList<CostUnitTypeDto>> GetAllAsync(CancellationToken ct = default);
}
