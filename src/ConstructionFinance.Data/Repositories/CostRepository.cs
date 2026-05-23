using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConstructionFinance.Data.Repositories;

public class CostRepository : Repository<CostItem>, ICostRepository
{
    public CostRepository(AppDbContext context) : base(context) { }

    public async Task<CostItem> AddNodeAsync(int projectId, int? parentId, CostItem newNode, CancellationToken ct = default)
    {
        newNode.ProjectId = projectId;
        newNode.ParentId = parentId;

        string parentPath;
        int parentLevel;
        if (parentId is null)
        {
            parentPath = "/";
            parentLevel = -1;
        }
        else
        {
            var parent = await DbSet.FirstOrDefaultAsync(c => c.Id == parentId, ct)
                ?? throw new InvalidOperationException($"Parent {parentId} not found");
            parentPath = parent.Path;
            parentLevel = parent.Level;
        }

        newNode.Level = parentLevel + 1;
        newNode.Path = parentPath;

        await DbSet.AddAsync(newNode, ct);
        await Context.SaveChangesAsync(ct);

        newNode.Path = $"{parentPath.TrimEnd('/')}/{newNode.Id}/";
        return newNode;
    }

    public async Task<IReadOnlyList<CostItem>> GetTreeByProjectAsync(int projectId, CancellationToken ct = default) =>
        await DbSet
            .Where(c => c.ProjectId == projectId)
            .OrderBy(c => c.Path)
            .ThenBy(c => c.SortOrder)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CostItem>> GetSubtreeAsync(int nodeId, CancellationToken ct = default)
    {
        var root = await DbSet.AsNoTracking().FirstOrDefaultAsync(c => c.Id == nodeId, ct)
            ?? throw new InvalidOperationException($"Node {nodeId} not found");
        return await DbSet
            .Where(c => c.Path.StartsWith(root.Path))
            .OrderBy(c => c.Path)
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
