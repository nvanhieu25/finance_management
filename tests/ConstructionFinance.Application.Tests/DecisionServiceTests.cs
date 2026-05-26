using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Data;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Validators;
using FluentValidation;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class DecisionServiceTests
{
    private static IDecisionService CreateService(AppDbContext ctx) =>
        new DecisionService(
            new Repository<ApprovalDecision>(ctx),
            new Repository<Contract>(ctx),
            new UnitOfWork(ctx),
            new ApprovalDecisionValidator());

    private static CreateDecisionDto ValidCreate(string code = "1234/QD") =>
        new(code, "Phê duyệt dự án", new DateOnly(2026, 5, 1), "UBND Tỉnh", 50_000_000_000m, null);

    [Fact]
    public async Task CreateAsync_persists()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        var dto = await svc.CreateAsync(ValidCreate());

        dto.Id.ShouldBeGreaterThan(0);
        dto.DecisionCode.ShouldBe("1234/QD");
    }

    [Fact]
    public async Task CreateAsync_invalid_when_amount_negative()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        await Should.ThrowAsync<ValidationException>(async () =>
            await svc.CreateAsync(new CreateDecisionDto("X", "T", new DateOnly(2026, 5, 1), "A", -1m, null)));
    }

    [Fact]
    public async Task DeleteAsync_blocked_when_contracts_exist()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);
        var decision = await svc.CreateAsync(ValidCreate());
        h.Context.Contracts.Add(new Contract { ApprovalDecisionId = decision.Id, ContractCode = "C1", ContractName = "C", ContractDate = new DateOnly(2026, 5, 1), ContractValue = 1m });
        await h.Context.SaveChangesAsync();

        await Should.ThrowAsync<BusinessRuleException>(async () => await svc.DeleteAsync(decision.Id));
    }

    [Fact]
    public async Task DeleteAsync_succeeds_when_no_contracts()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);
        var decision = await svc.CreateAsync(ValidCreate());

        await svc.DeleteAsync(decision.Id);

        (await svc.GetAllAsync()).ShouldBeEmpty();
    }
}
