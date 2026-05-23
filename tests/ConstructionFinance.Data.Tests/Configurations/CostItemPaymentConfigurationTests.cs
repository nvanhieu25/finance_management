using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Configurations;

public class CostItemPaymentConfigurationTests
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
    public void CostItem_self_ref_works()
    {
        var (ctx, projectId) = BootstrapProject();
        var root = new CostItem { ProjectId = projectId, ItemName = "Root", Path = "/", Level = 0, IsLeaf = false };
        ctx.CostItems.Add(root);
        ctx.SaveChanges();
        ctx.CostItems.Add(new CostItem { ProjectId = projectId, ParentId = root.Id, ItemName = "Child", Path = $"/{root.Id}/", Level = 1, IsLeaf = true });
        ctx.SaveChanges();

        ctx.CostItems.Count().ShouldBe(2);
        var child = ctx.CostItems.Include(c => c.Parent).Single(c => c.ItemName == "Child");
        child.Parent.ShouldNotBeNull();
        child.Parent!.ItemName.ShouldBe("Root");
    }

    [Fact]
    public void Payment_FK_to_CostItem_cascades_delete()
    {
        var (ctx, projectId) = BootstrapProject();
        var item = new CostItem { ProjectId = projectId, ItemName = "X", Path = "/", Level = 0, IsLeaf = true };
        ctx.CostItems.Add(item);
        ctx.SaveChanges();
        ctx.Payments.Add(new Payment { CostItemId = item.Id, PaymentNo = 1, PaymentType = PaymentType.Advance, PaymentDate = new DateOnly(2026, 5, 1), Amount = 100m, Percentage = 10m });
        ctx.SaveChanges();
        ctx.Payments.Count().ShouldBe(1);

        ctx.CostItems.Remove(item);
        ctx.SaveChanges();

        ctx.Payments.Count().ShouldBe(0);
    }

    [Fact]
    public void Payment_PaymentNo_unique_per_CostItem()
    {
        var (ctx, projectId) = BootstrapProject();
        var item = new CostItem { ProjectId = projectId, ItemName = "X", Path = "/", Level = 0, IsLeaf = true };
        ctx.CostItems.Add(item);
        ctx.SaveChanges();
        ctx.Payments.Add(new Payment { CostItemId = item.Id, PaymentNo = 1, PaymentType = PaymentType.Advance, PaymentDate = new DateOnly(2026, 5, 1), Amount = 100m, Percentage = 10m });
        ctx.SaveChanges();
        ctx.Payments.Add(new Payment { CostItemId = item.Id, PaymentNo = 1, PaymentType = PaymentType.Installment, PaymentDate = new DateOnly(2026, 6, 1), Amount = 200m, Percentage = 20m });

        Should.Throw<DbUpdateException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void CostItem_decimal_quantity_precision_4()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var prop = ctx.Model.FindEntityType(typeof(CostItem))!.FindProperty(nameof(CostItem.Quantity))!;
        prop.GetPrecision().ShouldBe(18);
        prop.GetScale().ShouldBe(4);
    }
}
