using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Repositories;

public class CostRepositoryTests
{
    private static (AppDbContext ctx, int projectId) BootstrapProject()
    {
        var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var d = new ApprovalDecision { DecisionCode = "D1", DecisionTitle = "X", IssuingAuthority = "U", DecisionDate = new DateOnly(2026, 1, 1), TotalApprovedAmount = 1m };
        ctx.ApprovalDecisions.Add(d);
        ctx.SaveChanges();
        var c = new Contract { ApprovalDecisionId = d.Id, ContractCode = "C1", ContractName = "C", ContractDate = new DateOnly(2026, 1, 1), ContractValue = 1m };
        ctx.Contracts.Add(c);
        ctx.SaveChanges();
        var p = new Project { ContractId = c.Id, ProjectCode = "P1", ProjectName = "P", Status = ProjectStatus.Planning };
        ctx.Projects.Add(p);
        ctx.SaveChanges();
        return (ctx, p.Id);
    }

    [Fact]
    public async Task AddRootAsync_sets_path_and_level_correctly()
    {
        var (ctx, projectId) = BootstrapProject();
        ICostRepository repo = new CostRepository(ctx);

        var root = await repo.AddNodeAsync(projectId, null, new CostItem
        {
            ProjectId = projectId,
            ItemName = "Chi phí xây dựng",
            Path = "",
            Level = 0,
            IsLeaf = false
        });
        await ctx.SaveChangesAsync();

        root.Level.ShouldBe(0);
        root.Path.ShouldBe($"/{root.Id}/");
    }

    [Fact]
    public async Task AddChildAsync_inherits_parent_path_and_increments_level()
    {
        var (ctx, projectId) = BootstrapProject();
        ICostRepository repo = new CostRepository(ctx);
        var root = await repo.AddNodeAsync(projectId, null, new CostItem
        {
            ProjectId = projectId,
            ItemName = "R",
            Path = "",
            Level = 0,
            IsLeaf = false
        });
        await ctx.SaveChangesAsync();

        var child = await repo.AddNodeAsync(projectId, root.Id, new CostItem
        {
            ProjectId = projectId,
            ItemName = "C",
            Path = "",
            Level = 0,
            IsLeaf = true
        });
        await ctx.SaveChangesAsync();

        child.Level.ShouldBe(1);
        child.Path.ShouldBe($"/{root.Id}/{child.Id}/");
        child.ParentId.ShouldBe(root.Id);
    }

    [Fact]
    public async Task GetTreeByProjectAsync_returns_all_items_for_project()
    {
        var (ctx, projectId) = BootstrapProject();
        ICostRepository repo = new CostRepository(ctx);
        var r = await repo.AddNodeAsync(projectId, null, new CostItem { ProjectId = projectId, ItemName = "R", Path = "", Level = 0, IsLeaf = false });
        await ctx.SaveChangesAsync();
        await repo.AddNodeAsync(projectId, r.Id, new CostItem { ProjectId = projectId, ItemName = "A", Path = "", Level = 0, IsLeaf = true });
        await repo.AddNodeAsync(projectId, r.Id, new CostItem { ProjectId = projectId, ItemName = "B", Path = "", Level = 0, IsLeaf = true });
        await ctx.SaveChangesAsync();

        var all = await repo.GetTreeByProjectAsync(projectId);
        all.Count.ShouldBe(3);
        all.Select(x => x.ItemName).ShouldBe(new[] { "R", "A", "B" }, ignoreOrder: true);
    }

    [Fact]
    public async Task GetSubtreeAsync_returns_only_node_and_descendants()
    {
        var (ctx, projectId) = BootstrapProject();
        ICostRepository repo = new CostRepository(ctx);
        var r1 = await repo.AddNodeAsync(projectId, null, new CostItem { ProjectId = projectId, ItemName = "R1", Path = "", Level = 0, IsLeaf = false });
        await ctx.SaveChangesAsync();
        await repo.AddNodeAsync(projectId, r1.Id, new CostItem { ProjectId = projectId, ItemName = "C1", Path = "", Level = 0, IsLeaf = true });
        await ctx.SaveChangesAsync();
        var r2 = await repo.AddNodeAsync(projectId, null, new CostItem { ProjectId = projectId, ItemName = "R2", Path = "", Level = 0, IsLeaf = false });
        await ctx.SaveChangesAsync();

        var sub = await repo.GetSubtreeAsync(r1.Id);

        sub.Select(x => x.ItemName).ShouldBe(new[] { "R1", "C1" }, ignoreOrder: true);
    }
}
