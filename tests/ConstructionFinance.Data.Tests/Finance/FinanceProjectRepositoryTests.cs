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

    [Fact]
    public async Task Repository_adds_lists_and_loads_with_lines()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new ConstructionFinance.Data.Repositories.FinanceProjectRepository(ctx);

        var p = new Project { Name = "DA1", ContractValue = 1_000m };
        p.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 1, 5), PaymentKLHT = 800m, RecoveredAdvance = 200m, DisbursedNSNN = 600m });
        var id = await repo.AddAsync(p);
        ctx.ChangeTracker.Clear();

        (await repo.ListAsync()).Count.ShouldBe(1);
        var loaded = await repo.GetWithLinesAsync(id);
        loaded.ShouldNotBeNull();
        loaded!.Lines.Count.ShouldBe(1);

        await repo.DeleteAsync(id);
        ctx.ChangeTracker.Clear();
        (await repo.ListAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_replaces_lines_and_updates_scalars()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new ConstructionFinance.Data.Repositories.FinanceProjectRepository(ctx);

        var p = new Project { Name = "DA1", ContractValue = 1_000m };
        p.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 1, 5), Advance = 500m, DisbursedNSNN = 500m });
        var id = await repo.AddAsync(p);
        ctx.ChangeTracker.Clear();

        var edited = new Project { Id = id, Name = "DA1 (sửa)", ContractValue = 2_000m };
        edited.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 3, 9), PaymentKLHT = 700m, RecoveredAdvance = 100m, DisbursedNSNN = 600m });
        edited.Lines.Add(new ProjectLineItem { SortOrder = 2, Date = new DateOnly(2026, 4, 1), DisbursedNgoaiHoi = 50m });
        await repo.UpdateAsync(edited);
        ctx.ChangeTracker.Clear();

        var loaded = await repo.GetWithLinesAsync(id);
        loaded.ShouldNotBeNull();
        loaded!.Name.ShouldBe("DA1 (sửa)");
        loaded.ContractValue.ShouldBe(2_000m);
        loaded.Lines.Count.ShouldBe(2);
        loaded.Lines.OrderBy(l => l.SortOrder).First().PaymentKLHT.ShouldBe(700m);

        // old lines fully gone — only the 2 new rows remain in the table
        ctx.FinanceProjectLines.Count(l => l.ProjectId == id).ShouldBe(2);
    }

    [Fact]
    public async Task ListWithLinesAsync_returns_all_projects_with_their_lines()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new ConstructionFinance.Data.Repositories.FinanceProjectRepository(ctx);

        var a = new Project { Name = "Beta", ContractValue = 100m };
        a.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 1, 1), Advance = 50m, DisbursedNSNN = 50m });
        var b = new Project { Name = "Alpha", ContractValue = 200m };
        b.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 2, 1), PaymentKLHT = 80m, DisbursedNSNN = 80m });
        b.Lines.Add(new ProjectLineItem { SortOrder = 2, Date = new DateOnly(2026, 3, 1), DisbursedNgoaiHoi = 20m });
        await repo.AddAsync(a);
        await repo.AddAsync(b);
        ctx.ChangeTracker.Clear();

        var all = await repo.ListWithLinesAsync();
        all.Count.ShouldBe(2);
        all[0].Name.ShouldBe("Alpha");           // ordered by Name
        all[0].Lines.Count.ShouldBe(2);
        all[1].Name.ShouldBe("Beta");
        all[1].Lines.Single().Advance.ShouldBe(50m);
    }
}
