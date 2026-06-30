using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;
using FinanceProject = ConstructionFinance.Domain.Finance.Project;

namespace ConstructionFinance.Data.Tests.Finance;

public class FinanceProjectDecisionFilterTests
{
    [Fact]
    public void Link_to_soft_deleted_decision_is_filtered_out()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();

        var d = new ApprovalDecision { DecisionCode = "D1", DecisionTitle = "X", IssuingAuthority = "U", DecisionDate = new DateOnly(2026, 1, 1), TotalApprovedAmount = 1m };
        ctx.ApprovalDecisions.Add(d);
        ctx.SaveChanges();
        var p = new FinanceProject { Name = "DA1" };
        ctx.FinanceProjects.Add(p);
        ctx.SaveChanges();
        ctx.FinanceProjectDecisions.Add(new ProjectDecision { ProjectId = p.Id, ApprovalDecisionId = d.Id });
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();

        ctx.FinanceProjectDecisions.Count().ShouldBe(1);   // visible while decision is alive

        var dec = ctx.ApprovalDecisions.Single();
        dec.DeletedAt = DateTime.UtcNow;
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();

        ctx.FinanceProjectDecisions.Count().ShouldBe(0);   // filtered with its soft-deleted decision
    }
}
