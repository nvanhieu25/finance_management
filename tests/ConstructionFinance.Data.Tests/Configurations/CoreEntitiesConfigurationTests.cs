using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Configurations;

public class CoreEntitiesConfigurationTests
{
    private static ApprovalDecision NewDecision(string code) => new()
    {
        DecisionCode = code,
        DecisionTitle = "T",
        IssuingAuthority = "A",
        DecisionDate = new DateOnly(2026, 5, 1),
        TotalApprovedAmount = 100m
    };

    [Fact]
    public void ApprovalDecision_DecisionCode_is_unique()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        ctx.ApprovalDecisions.Add(NewDecision("X"));
        ctx.SaveChanges();
        ctx.ApprovalDecisions.Add(NewDecision("X"));

        Should.Throw<DbUpdateException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void Contract_FK_to_Decision_cascades_restrict()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var decision = NewDecision("1234");
        ctx.ApprovalDecisions.Add(decision);
        ctx.SaveChanges();

        ctx.Contracts.Add(new Contract
        {
            ApprovalDecisionId = decision.Id,
            ContractCode = "HD-1",
            ContractName = "HD",
            ContractDate = new DateOnly(2026, 5, 1),
            ContractValue = 100m
        });
        ctx.SaveChanges();

        // Removing a Decision that has tracked Contracts should fail (either at
        // change-tracker time as InvalidOperationException, because the relationship
        // is required and Restrict prevents EF from setting FK to null, or at
        // DB time as DbUpdateException if the change tracker allows it through).
        Should.Throw<Exception>(() =>
        {
            ctx.ApprovalDecisions.Remove(decision);
            ctx.SaveChanges();
        }).ShouldSatisfyAllConditions(
            ex => ex.ShouldBeAssignableTo<Exception>(),
            ex => (ex is DbUpdateException || ex is InvalidOperationException).ShouldBeTrue());
    }

    [Fact]
    public void Contract_unique_code_per_decision()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var d = NewDecision("D1");
        ctx.ApprovalDecisions.Add(d);
        ctx.SaveChanges();

        ctx.Contracts.Add(new Contract { ApprovalDecisionId = d.Id, ContractCode = "HD-1", ContractName = "A", ContractDate = new DateOnly(2026, 5, 1), ContractValue = 1 });
        ctx.SaveChanges();
        ctx.Contracts.Add(new Contract { ApprovalDecisionId = d.Id, ContractCode = "HD-1", ContractName = "B", ContractDate = new DateOnly(2026, 5, 1), ContractValue = 1 });

        Should.Throw<DbUpdateException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void Project_status_stored_as_string()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var prop = ctx.Model.FindEntityType(typeof(Project))!.FindProperty(nameof(Project.Status))!;
        prop.GetProviderClrType().ShouldBe(typeof(string));
    }

    [Fact]
    public void Decimal_precision_on_money_fields()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var prop = ctx.Model.FindEntityType(typeof(ApprovalDecision))!
            .FindProperty(nameof(ApprovalDecision.TotalApprovedAmount))!;
        prop.GetPrecision().ShouldBe(18);
        prop.GetScale().ShouldBe(2);
    }
}
