using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Application.Mapping;

public static class CostUnitTypeMappings
{
    public static CostUnitTypeDto ToDto(this CostUnitType e) =>
        new(e.Id, e.Code, e.Name, e.Description);
}
