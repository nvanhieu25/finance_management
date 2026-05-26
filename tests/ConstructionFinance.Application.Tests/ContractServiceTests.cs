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

public class ContractServiceTests
{
    private static IContractService CreateService(AppDbContext ctx) =>
        new ContractService(
            new Repository<Contract>(ctx),
            new Repository<ApprovalDecision>(ctx),
            new Repository<Project>(ctx),
            new UnitOfWork(ctx),
            new ContractValidator());

    private static async Task<int> SeedDecision(AppDbContext ctx, string code = "D1")
    {
        var d = new ApprovalDecision { DecisionCode = code, DecisionTitle = "X", IssuingAuthority = "U", DecisionDate = new DateOnly(2026, 1, 1), TotalApprovedAmount = 1m };
        ctx.ApprovalDecisions.Add(d);
        await ctx.SaveChangesAsync();
        return d.Id;
    }

    [Fact]
    public async Task CreateAsync_succeeds_with_existing_decision()
    {
        using var h = new ServiceTestHarness();
        var decisionId = await SeedDecision(h.Context);
        var svc = CreateService(h.Context);

        var dto = await svc.CreateAsync(new CreateContractDto(decisionId, null, "HD-1", "Hợp đồng A", new DateOnly(2026, 5, 1), null, 1_000_000m, null));

        dto.Id.ShouldBeGreaterThan(0);
        dto.ContractCode.ShouldBe("HD-1");
    }

    [Fact]
    public async Task CreateAsync_throws_NotFound_when_decision_missing()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        await Should.ThrowAsync<NotFoundException>(async () =>
            await svc.CreateAsync(new CreateContractDto(999, null, "HD-1", "A", new DateOnly(2026, 5, 1), null, 1m, null)));
    }

    [Fact]
    public async Task DeleteAsync_blocked_when_projects_exist()
    {
        using var h = new ServiceTestHarness();
        var decisionId = await SeedDecision(h.Context);
        var svc = CreateService(h.Context);
        var contract = await svc.CreateAsync(new CreateContractDto(decisionId, null, "HD-1", "A", new DateOnly(2026, 5, 1), null, 1m, null));
        h.Context.Projects.Add(new Project { ContractId = contract.Id, ProjectCode = "P1", ProjectName = "P", Status = ProjectStatus.Planning });
        await h.Context.SaveChangesAsync();

        await Should.ThrowAsync<BusinessRuleException>(async () => await svc.DeleteAsync(contract.Id));
    }

    [Fact]
    public async Task GetByDecisionAsync_returns_only_contracts_of_that_decision()
    {
        using var h = new ServiceTestHarness();
        var d1 = await SeedDecision(h.Context, "D1");
        var d2 = await SeedDecision(h.Context, "D2");
        var svc = CreateService(h.Context);
        await svc.CreateAsync(new CreateContractDto(d1, null, "HD-1", "A", new DateOnly(2026, 5, 1), null, 1m, null));
        await svc.CreateAsync(new CreateContractDto(d1, null, "HD-2", "B", new DateOnly(2026, 5, 1), null, 1m, null));
        await svc.CreateAsync(new CreateContractDto(d2, null, "HD-3", "C", new DateOnly(2026, 5, 1), null, 1m, null));

        var d1Contracts = await svc.GetByDecisionAsync(d1);

        d1Contracts.Count.ShouldBe(2);
    }
}
