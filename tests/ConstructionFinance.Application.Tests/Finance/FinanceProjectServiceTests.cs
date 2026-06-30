using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Finance;

public class FinanceProjectServiceTests
{
    [Fact]
    public async Task GetAggregateAsync_computes_from_saved_lines()
    {
        using var h = new ServiceTestHarness();
        var repo = new FinanceProjectRepository(h.Context);
        var svc = new FinanceProjectService(repo);

        var p = new Project { Name = "DA1", ContractValue = 1_000m };
        p.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 1, 5), Advance = 500m, DisbursedNSNN = 500m });
        p.Lines.Add(new ProjectLineItem { SortOrder = 2, Date = new DateOnly(2026, 2, 20), PaymentKLHT = 800m, RecoveredAdvance = 200m, DisbursedNSNN = 420m, DisbursedNgoaiHoi = 180m });
        var id = await svc.CreateAsync(p);
        h.Context.ChangeTracker.Clear();

        var agg = await svc.GetAggregateAsync(id);

        agg.ShouldNotBeNull();
        agg!.UnrecoveredAdvance.ShouldBe(300m);  // 500 − 200
        agg.TotalDisbursed.ShouldBe(1_100m);     // 500 + 600
        agg.SumNSNN.ShouldBe(920m);
        agg.SumNgoaiHoi.ShouldBe(180m);
    }

    [Fact]
    public async Task GetAggregateAsync_returns_null_for_missing_project()
    {
        using var h = new ServiceTestHarness();
        var svc = new FinanceProjectService(new FinanceProjectRepository(h.Context));
        (await svc.GetAggregateAsync(999)).ShouldBeNull();
    }
}
