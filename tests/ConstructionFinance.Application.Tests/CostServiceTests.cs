using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Data;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using ConstructionFinance.Domain.Validators;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class CostServiceTests
{
    private static ICostService CreateService(AppDbContext ctx) =>
        new CostService(
            new CostRepository(ctx),
            new Repository<Payment>(ctx),
            new UnitOfWork(ctx),
            new CostItemValidator());

    private static async Task<int> SeedProject(AppDbContext ctx)
    {
        var d = new ApprovalDecision { DecisionCode = "D1", DecisionTitle = "X", IssuingAuthority = "U", DecisionDate = new DateOnly(2026, 1, 1), TotalApprovedAmount = 1m };
        ctx.ApprovalDecisions.Add(d); await ctx.SaveChangesAsync();
        var c = new Contract { ApprovalDecisionId = d.Id, ContractCode = "C1", ContractName = "C", ContractDate = new DateOnly(2026, 1, 1), ContractValue = 1m };
        ctx.Contracts.Add(c); await ctx.SaveChangesAsync();
        var p = new Project { ContractId = c.Id, ProjectCode = "P1", ProjectName = "P", Status = ProjectStatus.InProgress };
        ctx.Projects.Add(p); await ctx.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task CreateNodeAsync_root_sets_path_level()
    {
        using var h = new ServiceTestHarness();
        var projectId = await SeedProject(h.Context);
        var svc = CreateService(h.Context);

        var root = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, null, null, "Chi phí xây dựng", false, null, 0, 0, 0, null, null));

        root.Level.ShouldBe(0);
        root.Path.ShouldBe($"/{root.Id}/");
        root.ParentId.ShouldBeNull();
    }

    [Fact]
    public async Task CreateNodeAsync_leaf_computes_estimated_from_qty_price()
    {
        using var h = new ServiceTestHarness();
        var projectId = await SeedProject(h.Context);
        var svc = CreateService(h.Context);
        var root = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, null, null, "R", false, null, 0, 0, 0, null, null));

        var leaf = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, root.Id, null, "Móng", true, null, 120m, 25_000_000m, 0, null, null));

        leaf.EstimatedAmount.ShouldBe(3_000_000_000m); // 120 * 25,000,000
    }

    [Fact]
    public async Task UpdateNodeAsync_recomputes_leaf_estimated()
    {
        using var h = new ServiceTestHarness();
        var projectId = await SeedProject(h.Context);
        var svc = CreateService(h.Context);
        var leaf = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, null, null, "Móng", true, null, 10m, 1_000m, 0, null, null));

        var updated = await svc.UpdateNodeAsync(new UpdateCostItemDto(leaf.Id, null, null, "Móng", true, null, 20m, 1_000m, 0, 0, null));

        updated.EstimatedAmount.ShouldBe(20_000m);
    }

    [Fact]
    public async Task RecalculateRollupAsync_sums_children_into_parents()
    {
        using var h = new ServiceTestHarness();
        var projectId = await SeedProject(h.Context);
        var svc = CreateService(h.Context);
        var root = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, null, null, "Xây dựng", false, null, 0, 0, 0, null, null));
        var phanTho = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, root.Id, null, "Phần thô", false, null, 0, 0, 0, null, null));
        await svc.CreateNodeAsync(new CreateCostItemDto(projectId, phanTho.Id, null, "Móng", true, null, 0, 0, 3_000_000_000m, null, null));
        await svc.CreateNodeAsync(new CreateCostItemDto(projectId, phanTho.Id, null, "Cột", true, null, 0, 0, 2_400_000_000m, null, null));

        await svc.RecalculateRollupAsync(projectId);

        var tree = await svc.GetProjectTreeAsync(projectId);
        tree.Single(x => x.ItemName == "Phần thô").EstimatedAmount.ShouldBe(5_400_000_000m);
        tree.Single(x => x.ItemName == "Xây dựng").EstimatedAmount.ShouldBe(5_400_000_000m);
    }

    [Fact]
    public async Task GetProjectTreeAsync_returns_nodes_ordered_by_path()
    {
        using var h = new ServiceTestHarness();
        var projectId = await SeedProject(h.Context);
        var svc = CreateService(h.Context);
        var root = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, null, null, "R", false, null, 0, 0, 0, null, null));
        await svc.CreateNodeAsync(new CreateCostItemDto(projectId, root.Id, null, "A", true, null, 0, 0, 0, null, null));

        var tree = await svc.GetProjectTreeAsync(projectId);

        tree.Count.ShouldBe(2);
        tree[0].ItemName.ShouldBe("R");
    }
}
