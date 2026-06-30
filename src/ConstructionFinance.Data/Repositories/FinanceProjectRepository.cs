using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ConstructionFinance.Data.Repositories;

public class FinanceProjectRepository : IFinanceProjectRepository
{
    private readonly AppDbContext _context;

    public FinanceProjectRepository(AppDbContext context) => _context = context;

    public async Task<int> AddAsync(Project project, CancellationToken ct = default)
    {
        await _context.FinanceProjects.AddAsync(project, ct);
        await _context.SaveChangesAsync(ct);
        return project.Id;
    }

    public async Task<Project?> GetWithLinesAsync(int id, CancellationToken ct = default) =>
        await _context.FinanceProjects
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default) =>
        await _context.FinanceProjects.OrderBy(p => p.Name).ToListAsync(ct);

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var p = await _context.FinanceProjects.FindAsync(new object[] { id }, ct);
        if (p is not null)
        {
            _context.FinanceProjects.Remove(p);
            await _context.SaveChangesAsync(ct);
        }
    }
}
