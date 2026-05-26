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

public class PaymentServiceTests
{
    private static IPaymentService CreateService(AppDbContext ctx) =>
        new PaymentService(
            new Repository<Payment>(ctx),
            new Repository<CostItem>(ctx),
            new UnitOfWork(ctx),
            new PaymentValidator());

    private static async Task<int> SeedCostItem(AppDbContext ctx, decimal estimated = 3_000_000_000m)
    {
        var d = new ApprovalDecision { DecisionCode = "D1", DecisionTitle = "X", IssuingAuthority = "U", DecisionDate = new DateOnly(2026, 1, 1), TotalApprovedAmount = 1m };
        ctx.ApprovalDecisions.Add(d); await ctx.SaveChangesAsync();
        var c = new Contract { ApprovalDecisionId = d.Id, ContractCode = "C1", ContractName = "C", ContractDate = new DateOnly(2026, 1, 1), ContractValue = 1m };
        ctx.Contracts.Add(c); await ctx.SaveChangesAsync();
        var p = new Project { ContractId = c.Id, ProjectCode = "P1", ProjectName = "P", Status = ProjectStatus.InProgress };
        ctx.Projects.Add(p); await ctx.SaveChangesAsync();
        var ci = new CostItem { ProjectId = p.Id, ItemName = "Móng", Path = "/", Level = 0, IsLeaf = true, EstimatedAmount = estimated };
        ctx.CostItems.Add(ci); await ctx.SaveChangesAsync();
        return ci.Id;
    }

    [Fact]
    public async Task AddAsync_auto_assigns_payment_no_sequentially()
    {
        using var h = new ServiceTestHarness();
        var ciId = await SeedCostItem(h.Context);
        var svc = CreateService(h.Context);

        var p1 = await svc.AddAsync(new CreatePaymentDto(ciId, PaymentType.Advance, new DateOnly(2026, 4, 10), 900_000_000m, null, "HD-1", null, null, null));
        var p2 = await svc.AddAsync(new CreatePaymentDto(ciId, PaymentType.Installment, new DateOnly(2026, 5, 10), 600_000_000m, null, "HD-2", null, null, null));

        p1.PaymentNo.ShouldBe(1);
        p2.PaymentNo.ShouldBe(2);
    }

    [Fact]
    public async Task AddAsync_auto_computes_percentage_from_estimated()
    {
        using var h = new ServiceTestHarness();
        var ciId = await SeedCostItem(h.Context, estimated: 3_000_000_000m);
        var svc = CreateService(h.Context);

        var p = await svc.AddAsync(new CreatePaymentDto(ciId, PaymentType.Advance, new DateOnly(2026, 4, 10), 900_000_000m, null, null, null, null, null));

        p.Percentage.ShouldBe(30m); // 900M / 3000M * 100
    }

    [Fact]
    public async Task AddAsync_throws_NotFound_when_cost_item_missing()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        await Should.ThrowAsync<NotFoundException>(async () =>
            await svc.AddAsync(new CreatePaymentDto(999, PaymentType.Advance, new DateOnly(2026, 4, 10), 1m, null, null, null, null, null)));
    }

    [Fact]
    public async Task GetSummaryAsync_computes_totals_and_completion()
    {
        using var h = new ServiceTestHarness();
        var ciId = await SeedCostItem(h.Context, estimated: 3_000_000_000m);
        var svc = CreateService(h.Context);
        await svc.AddAsync(new CreatePaymentDto(ciId, PaymentType.Advance, new DateOnly(2026, 4, 10), 900_000_000m, null, null, null, null, null));
        await svc.AddAsync(new CreatePaymentDto(ciId, PaymentType.Installment, new DateOnly(2026, 5, 10), 600_000_000m, null, null, null, null, null));

        var summary = await svc.GetSummaryAsync(ciId);

        summary.EstimatedAmount.ShouldBe(3_000_000_000m);
        summary.TotalPaid.ShouldBe(1_500_000_000m);
        summary.Remaining.ShouldBe(1_500_000_000m);
        summary.CompletionPct.ShouldBe(50m);
    }
}
