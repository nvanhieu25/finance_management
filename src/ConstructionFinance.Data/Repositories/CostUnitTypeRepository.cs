using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConstructionFinance.Data.Repositories;

public class CostUnitTypeRepository : ICostUnitTypeRepository
{
    private readonly AppDbContext _context;

    public CostUnitTypeRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<CostUnitType>> ListAsync(CancellationToken ct = default) =>
        await _context.CostUnitTypes.AsNoTracking().ToListAsync(ct);
}
