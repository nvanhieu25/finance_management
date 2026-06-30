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

    public async Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        var existing = await _context.FinanceProjects
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == project.Id, ct);
        if (existing is null) return;

        existing.Code = project.Code;
        existing.Name = project.Name;
        existing.ContractorName = project.ContractorName;
        existing.ContractValue = project.ContractValue;
        existing.Notes = project.Notes;

        // Replace strategy: drop all current lines, re-add the incoming ones as
        // fresh rows. Single-user, small data — simplest correct approach and it
        // sidesteps identity-matching of edited rows.
        _context.FinanceProjectLines.RemoveRange(existing.Lines);
        existing.Lines.Clear();
        foreach (var line in project.Lines)
        {
            existing.Lines.Add(new ProjectLineItem
            {
                SortOrder = line.SortOrder,
                Date = line.Date,
                PackageName = line.PackageName,
                ContractRef = line.ContractRef,
                Advance = line.Advance,
                PaymentKLHT = line.PaymentKLHT,
                RecoveredAdvance = line.RecoveredAdvance,
                DisbursedNSNN = line.DisbursedNSNN,
                DisbursedNgoaiHoi = line.DisbursedNgoaiHoi
            });
        }
        await _context.SaveChangesAsync(ct);
    }

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
