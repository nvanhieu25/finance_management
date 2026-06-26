using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Configurations;

public class PaymentQueryFilterTests
{
    [Fact]
    public void Payments_of_soft_deleted_cost_item_are_filtered_out()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();

        var d = new ApprovalDecision { DecisionCode = "D1", DecisionTitle = "X", IssuingAuthority = "U", DecisionDate = new DateOnly(2026, 1, 1), TotalApprovedAmount = 1m };
        ctx.ApprovalDecisions.Add(d);
        ctx.SaveChanges();
        var c = new Contract { ApprovalDecisionId = d.Id, ContractCode = "C1", ContractName = "C", ContractDate = new DateOnly(2026, 1, 1), ContractValue = 1m };
        ctx.Contracts.Add(c);
        ctx.SaveChanges();
        var p = new Project { ContractId = c.Id, ProjectCode = "P1", ProjectName = "P", Status = ProjectStatus.Planning };
        ctx.Projects.Add(p);
        ctx.SaveChanges();
        var item = new CostItem { ProjectId = p.Id, ItemName = "Item", Path = "/1/", Level = 0, IsLeaf = true };
        ctx.Set<CostItem>().Add(item);
        ctx.SaveChanges();
        var pay = new Payment { CostItemId = item.Id, PaymentNo = 1, PaymentType = PaymentType.Advance, PaymentDate = new DateOnly(2026, 1, 1), Amount = 100m, Percentage = 10m };
        ctx.Set<Payment>().Add(pay);
        ctx.SaveChanges();

        // Sanity: visible while the cost item is alive.
        ctx.ChangeTracker.Clear();
        ctx.Set<Payment>().Count().ShouldBe(1);

        // Soft-delete the cost item (only sets DeletedAt; no cascade on soft-delete).
        var ci = ctx.Set<CostItem>().Single();
        ci.DeletedAt = DateTime.UtcNow;
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();

        // The payment must now be filtered out, consistent with its (soft-deleted) cost item.
        ctx.Set<Payment>().Count().ShouldBe(0);
    }
}
