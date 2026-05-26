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

public class ProjectServiceTests
{
    private static IProjectService CreateService(AppDbContext ctx) =>
        new ProjectService(
            new Repository<Project>(ctx),
            new Repository<Contract>(ctx),
            new Repository<CostItem>(ctx),
            new UnitOfWork(ctx),
            new ProjectValidator());

    private static async Task<int> SeedContract(AppDbContext ctx)
    {
        var d = new ApprovalDecision { DecisionCode = "D1", DecisionTitle = "X", IssuingAuthority = "U", DecisionDate = new DateOnly(2026, 1, 1), TotalApprovedAmount = 1m };
        ctx.ApprovalDecisions.Add(d);
        await ctx.SaveChangesAsync();
        var c = new Contract { ApprovalDecisionId = d.Id, ContractCode = "C1", ContractName = "C", ContractDate = new DateOnly(2026, 1, 1), ContractValue = 1m };
        ctx.Contracts.Add(c);
        await ctx.SaveChangesAsync();
        return c.Id;
    }

    private static CreateProjectDto ValidCreate(int contractId) =>
        new(contractId, "DA-1", "Nhà máy XYZ", "Dân dụng", "Hà Nội", null, null, 1_000_000m, null);

    [Fact]
    public async Task CreateAsync_defaults_status_Planning()
    {
        using var h = new ServiceTestHarness();
        var contractId = await SeedContract(h.Context);
        var svc = CreateService(h.Context);

        var dto = await svc.CreateAsync(ValidCreate(contractId));

        dto.Status.ShouldBe(ProjectStatus.Planning);
    }

    [Fact]
    public async Task UpdateStatus_Planning_to_InProgress_allowed()
    {
        using var h = new ServiceTestHarness();
        var contractId = await SeedContract(h.Context);
        var svc = CreateService(h.Context);
        var p = await svc.CreateAsync(ValidCreate(contractId));

        var updated = await svc.UpdateStatusAsync(p.Id, ProjectStatus.InProgress);

        updated.Status.ShouldBe(ProjectStatus.InProgress);
    }

    [Fact]
    public async Task UpdateStatus_Completed_to_InProgress_blocked()
    {
        using var h = new ServiceTestHarness();
        var contractId = await SeedContract(h.Context);
        var svc = CreateService(h.Context);
        var p = await svc.CreateAsync(ValidCreate(contractId));
        await svc.UpdateStatusAsync(p.Id, ProjectStatus.InProgress);
        await svc.UpdateStatusAsync(p.Id, ProjectStatus.Completed);

        await Should.ThrowAsync<BusinessRuleException>(async () =>
            await svc.UpdateStatusAsync(p.Id, ProjectStatus.InProgress));
    }

    [Fact]
    public async Task UpdateStatus_Planning_to_Completed_blocked()
    {
        using var h = new ServiceTestHarness();
        var contractId = await SeedContract(h.Context);
        var svc = CreateService(h.Context);
        var p = await svc.CreateAsync(ValidCreate(contractId));

        await Should.ThrowAsync<BusinessRuleException>(async () =>
            await svc.UpdateStatusAsync(p.Id, ProjectStatus.Completed));
    }

    [Fact]
    public async Task DeleteAsync_blocked_when_cost_items_exist()
    {
        using var h = new ServiceTestHarness();
        var contractId = await SeedContract(h.Context);
        var svc = CreateService(h.Context);
        var p = await svc.CreateAsync(ValidCreate(contractId));
        h.Context.CostItems.Add(new CostItem { ProjectId = p.Id, ItemName = "X", Path = "/", Level = 0, IsLeaf = true });
        await h.Context.SaveChangesAsync();

        await Should.ThrowAsync<BusinessRuleException>(async () => await svc.DeleteAsync(p.Id));
    }
}
