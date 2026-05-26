using ConstructionFinance.Application.Common.Exceptions;
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

public class CostServiceMoveDeleteTests
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
    public async Task MoveNodeAsync_reparents_and_rebuilds_subtree_paths()
    {
        using var h = new ServiceTestHarness();
        var projectId = await SeedProject(h.Context);
        var svc = CreateService(h.Context);
        var r1 = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, null, null, "R1", false, null, 0, 0, 0, null, null));
        var r2 = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, null, null, "R2", false, null, 0, 0, 0, null, null));
        var child = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, r1.Id, null, "Child", false, null, 0, 0, 0, null, null));
        var grandchild = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, child.Id, null, "GC", true, null, 0, 0, 0, null, null));

        await svc.MoveNodeAsync(child.Id, r2.Id);

        var tree = await svc.GetProjectTreeAsync(projectId);
        var movedChild = tree.Single(x => x.ItemName == "Child");
        var movedGc = tree.Single(x => x.ItemName == "GC");
        movedChild.ParentId.ShouldBe(r2.Id);
        movedChild.Path.ShouldBe($"/{r2.Id}/{child.Id}/");
        movedChild.Level.ShouldBe(1);
        movedGc.Path.ShouldBe($"/{r2.Id}/{child.Id}/{grandchild.Id}/");
        movedGc.Level.ShouldBe(2);
    }

    [Fact]
    public async Task MoveNodeAsync_into_own_subtree_is_blocked()
    {
        using var h = new ServiceTestHarness();
        var projectId = await SeedProject(h.Context);
        var svc = CreateService(h.Context);
        var root = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, null, null, "Root", false, null, 0, 0, 0, null, null));
        var child = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, root.Id, null, "Child", false, null, 0, 0, 0, null, null));

        await Should.ThrowAsync<BusinessRuleException>(async () => await svc.MoveNodeAsync(root.Id, child.Id));
    }

    [Fact]
    public async Task DeleteNodeAsync_soft_deletes_whole_subtree()
    {
        using var h = new ServiceTestHarness();
        var projectId = await SeedProject(h.Context);
        var svc = CreateService(h.Context);
        var root = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, null, null, "Root", false, null, 0, 0, 0, null, null));
        await svc.CreateNodeAsync(new CreateCostItemDto(projectId, root.Id, null, "Child", true, null, 0, 0, 0, null, null));

        await svc.DeleteNodeAsync(root.Id);

        (await svc.GetProjectTreeAsync(projectId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteNodeAsync_blocked_when_subtree_has_payments()
    {
        using var h = new ServiceTestHarness();
        var projectId = await SeedProject(h.Context);
        var svc = CreateService(h.Context);
        var root = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, null, null, "Root", false, null, 0, 0, 0, null, null));
        var leaf = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, root.Id, null, "Leaf", true, null, 0, 0, 1_000m, null, null));
        h.Context.Payments.Add(new Payment { CostItemId = leaf.Id, PaymentNo = 1, PaymentType = PaymentType.Advance, PaymentDate = new DateOnly(2026, 5, 1), Amount = 100m, Percentage = 10m });
        await h.Context.SaveChangesAsync();

        await Should.ThrowAsync<BusinessRuleException>(async () => await svc.DeleteNodeAsync(root.Id));
    }
}
