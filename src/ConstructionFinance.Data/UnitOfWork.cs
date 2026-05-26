using ConstructionFinance.Domain.Abstractions;

namespace ConstructionFinance.Data;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);
}
