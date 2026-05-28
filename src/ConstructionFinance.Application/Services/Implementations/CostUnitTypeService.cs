using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Mapping;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Abstractions;

namespace ConstructionFinance.Application.Services.Implementations;

public class CostUnitTypeService : ICostUnitTypeService
{
    private readonly ICostUnitTypeRepository _units;

    public CostUnitTypeService(ICostUnitTypeRepository units) => _units = units;

    public async Task<IReadOnlyList<CostUnitTypeDto>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _units.ListAsync(ct);
        return list.OrderBy(u => u.Code, StringComparer.Ordinal)
                   .Select(u => u.ToDto())
                   .ToList();
    }
}
