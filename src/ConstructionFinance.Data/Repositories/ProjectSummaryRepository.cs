using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConstructionFinance.Data.Repositories;

public class ProjectSummaryRepository : IProjectSummaryRepository
{
    private readonly AppDbContext _ctx;

    public ProjectSummaryRepository(AppDbContext ctx) => _ctx = ctx;

    public async Task<ImportBatch?> GetBatchByIdAsync(int batchId, CancellationToken ct = default) =>
        await _ctx.ImportBatches
            .Include(b => b.Summaries).ThenInclude(s => s.YearlySettlements)
            .FirstOrDefaultAsync(b => b.Id == batchId, ct);

    public async Task<IReadOnlyList<ImportBatch>> ListBatchesAsync(CancellationToken ct = default) =>
        await _ctx.ImportBatches.OrderByDescending(b => b.ImportedAtUtc).ToListAsync(ct);

    public async Task<ImportBatch?> GetLatestBatchAsync(CancellationToken ct = default) =>
        await _ctx.ImportBatches
            .Include(b => b.Summaries).ThenInclude(s => s.YearlySettlements)
            .OrderByDescending(b => b.ImportedAtUtc)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ImportBatch>> FindByHashAsync(string sourceFileHash, CancellationToken ct = default) =>
        await _ctx.ImportBatches
            .Where(b => b.SourceFileHash == sourceFileHash)
            .OrderByDescending(b => b.ImportedAtUtc)
            .ToListAsync(ct);

    public Task AddBatchAsync(ImportBatch batch, CancellationToken ct = default) =>
        _ctx.ImportBatches.AddAsync(batch, ct).AsTask();

    public async Task DeleteBatchAsync(int batchId, CancellationToken ct = default)
    {
        var batch = await _ctx.ImportBatches.FindAsync(new object[] { batchId }, ct);
        if (batch is not null) _ctx.ImportBatches.Remove(batch);
    }

    public async Task<IReadOnlyList<ProjectSummary>> ListByBatchAsync(int batchId, CancellationToken ct = default) =>
        await _ctx.ProjectSummaries
            .Include(s => s.YearlySettlements)
            .Where(s => s.ImportBatchId == batchId)
            .OrderBy(s => s.SheetName)
            .ToListAsync(ct);
}
