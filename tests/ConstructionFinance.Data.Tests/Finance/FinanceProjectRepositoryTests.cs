using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Finance;

public class FinanceProjectRepositoryTests
{
    [Fact]
    public void Context_round_trips_project_with_lines()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();

        var p = new Project { Name = "Cầu A12", ContractValue = 1_000m };
        p.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 1, 5), Advance = 500m, DisbursedNSNN = 500m });
        ctx.FinanceProjects.Add(p);
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();

        var loaded = ctx.FinanceProjects.Include(x => x.Lines).Single();
        loaded.Name.ShouldBe("Cầu A12");
        loaded.ContractValue.ShouldBe(1_000m);
        loaded.Lines.Count.ShouldBe(1);
        loaded.Lines[0].Advance.ShouldBe(500m);
        loaded.Lines[0].DisbursedNSNN.ShouldBe(500m);
    }
}
