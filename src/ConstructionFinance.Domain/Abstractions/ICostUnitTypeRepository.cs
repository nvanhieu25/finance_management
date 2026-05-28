using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Domain.Abstractions;

/// Read-only lookup repository for CostUnitType (reference data, no soft-delete or audit).
public interface ICostUnitTypeRepository
{
    Task<IReadOnlyList<CostUnitType>> ListAsync(CancellationToken ct = default);
}
