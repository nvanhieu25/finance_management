using ConstructionFinance.Application.Import;
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

    [Fact]
    public async Task Update_then_GetWithLines_reflects_changes()
    {
        using var h = new ServiceTestHarness();
        var svc = new FinanceProjectService(new FinanceProjectRepository(h.Context));

        var p = new Project { Name = "DA1", ContractValue = 1_000m };
        p.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 1, 5), Advance = 500m, DisbursedNSNN = 500m });
        var id = await svc.CreateAsync(p);
        h.Context.ChangeTracker.Clear();

        var edited = new Project { Id = id, Name = "DA1b", ContractValue = 3_000m };
        edited.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 2, 2), PaymentKLHT = 900m, DisbursedNSNN = 900m });
        await svc.UpdateAsync(edited);
        h.Context.ChangeTracker.Clear();

        (await svc.ListAsync()).Count.ShouldBe(1);
        var loaded = await svc.GetWithLinesAsync(id);
        loaded.ShouldNotBeNull();
        loaded!.ContractValue.ShouldBe(3_000m);
        loaded.Lines.Single().PaymentKLHT.ShouldBe(900m);

        await svc.DeleteAsync(id);
        h.Context.ChangeTracker.Clear();
        (await svc.ListAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllSummariesAsync_returns_one_computed_summary_per_project()
    {
        using var h = new ServiceTestHarness();
        var svc = new FinanceProjectService(new FinanceProjectRepository(h.Context));

        var p1 = new Project { Name = "DA1", ContractValue = 1_000m, ContractorName = "Nhà thầu A" };
        p1.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 1, 5), Advance = 500m, DisbursedNSNN = 500m });
        p1.Lines.Add(new ProjectLineItem { SortOrder = 2, Date = new DateOnly(2026, 2, 1), PaymentKLHT = 800m, RecoveredAdvance = 200m, DisbursedNSNN = 420m, DisbursedNgoaiHoi = 180m });
        var p2 = new Project { Name = "DA2", ContractValue = 2_000m };
        await svc.CreateAsync(p1);
        await svc.CreateAsync(p2);
        h.Context.ChangeTracker.Clear();

        var summaries = await svc.GetAllSummariesAsync();

        summaries.Count.ShouldBe(2);
        var s1 = summaries.Single(s => s.Name == "DA1");
        s1.ContractValue.ShouldBe(1_000m);
        s1.ContractorName.ShouldBe("Nhà thầu A");
        s1.Aggregate.UnrecoveredAdvance.ShouldBe(300m);   // 500 − 200
        s1.Aggregate.TotalDisbursed.ShouldBe(1_100m);     // (500) + (420+180)
        var s2 = summaries.Single(s => s.Name == "DA2");
        s2.Aggregate.TotalDisbursed.ShouldBe(0m);
    }

    [Fact]
    public async Task SaveImportedAsync_persists_all_projects_with_lines()
    {
        using var h = new ServiceTestHarness();
        var svc = new FinanceProjectService(new FinanceProjectRepository(h.Context));

        var p1 = new Project { Name = "DA1", ContractValue = 1_000m, ContractAnnexValue = 1_100m };
        p1.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 1, 5), Advance = 500m, DisbursedNSNN = 500m });
        var p2 = new Project { Name = "DA2", ContractValue = 2_000m };

        var count = await svc.SaveImportedAsync(new[] { p1, p2 });
        h.Context.ChangeTracker.Clear();

        count.ShouldBe(2);
        var summaries = await svc.GetAllSummariesAsync();
        summaries.Count.ShouldBe(2);
        summaries.Single(s => s.Name == "DA1").Aggregate.TotalDisbursed.ShouldBe(500m);
    }

    [Fact]
    public async Task SaveMergedAsync_skip_leaves_the_existing_project_untouched()
    {
        using var h = new ServiceTestHarness();
        var svc = new FinanceProjectService(new FinanceProjectRepository(h.Context));

        var original = new Project { Name = "DA1", ContractValue = 1_000m };
        original.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 1, 5), DisbursedNSNN = 500m });
        var id = await svc.CreateAsync(original);
        h.Context.ChangeTracker.Clear();

        // A parsed re-import of DA1 with different numbers, resolved as Skip.
        var parsed = new Project { Name = "DA1", ContractValue = 9_999m };
        parsed.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 2, 2), DisbursedNSNN = 42m });
        var written = await svc.SaveMergedAsync(new[]
        {
            new ImportCandidate(parsed, id, ImportResolution.Skip)
        });
        h.Context.ChangeTracker.Clear();

        written.ShouldBe(0);
        (await svc.ListAsync()).Count.ShouldBe(1);
        var loaded = await svc.GetWithLinesAsync(id);
        loaded!.ContractValue.ShouldBe(1_000m);                 // unchanged
        loaded.Lines.Single().DisbursedNSNN.ShouldBe(500m);    // original line kept
    }

    [Fact]
    public async Task SaveMergedAsync_overwrite_updates_the_existing_row_in_place()
    {
        using var h = new ServiceTestHarness();
        var svc = new FinanceProjectService(new FinanceProjectRepository(h.Context));

        var original = new Project { Name = "DA1", ContractValue = 1_000m };
        original.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 1, 5), DisbursedNSNN = 500m });
        var id = await svc.CreateAsync(original);
        h.Context.ChangeTracker.Clear();

        var parsed = new Project { Name = "DA1", ContractValue = 3_000m };
        parsed.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 2, 2), DisbursedNSNN = 700m });
        var written = await svc.SaveMergedAsync(new[]
        {
            new ImportCandidate(parsed, id, ImportResolution.Overwrite)
        });
        h.Context.ChangeTracker.Clear();

        written.ShouldBe(1);
        (await svc.ListAsync()).Count.ShouldBe(1);              // no duplicate
        var loaded = await svc.GetWithLinesAsync(id);
        loaded!.Id.ShouldBe(id);                               // same row
        loaded.ContractValue.ShouldBe(3_000m);                // updated
        loaded.Lines.Single().DisbursedNSNN.ShouldBe(700m);   // lines replaced
    }

    [Fact]
    public async Task SaveMergedAsync_overwrite_stores_the_trimmed_name()
    {
        using var h = new ServiceTestHarness();
        var svc = new FinanceProjectService(new FinanceProjectRepository(h.Context));

        var id = await svc.CreateAsync(new Project { Name = "DA1", ContractValue = 1_000m });
        h.Context.ChangeTracker.Clear();

        // Excel cell arrives padded; the stored name must stay "DA1".
        var parsed = new Project { Name = "  da1 ", ContractValue = 2_000m };
        await svc.SaveMergedAsync(new[]
        {
            new ImportCandidate(parsed, id, ImportResolution.Overwrite)
        });
        h.Context.ChangeTracker.Clear();

        (await svc.GetWithLinesAsync(id))!.Name.ShouldBe("da1");
    }

    [Fact]
    public async Task SaveMergedAsync_addnew_inserts_a_new_project()
    {
        using var h = new ServiceTestHarness();
        var svc = new FinanceProjectService(new FinanceProjectRepository(h.Context));

        await svc.CreateAsync(new Project { Name = "DA1", ContractValue = 1_000m });
        h.Context.ChangeTracker.Clear();

        var parsed = new Project { Name = "DA9", ContractValue = 5_000m };
        var written = await svc.SaveMergedAsync(new[]
        {
            new ImportCandidate(parsed, null, ImportResolution.AddNew)
        });
        h.Context.ChangeTracker.Clear();

        written.ShouldBe(1);
        var all = await svc.ListAsync();
        all.Count.ShouldBe(2);
        all.Select(p => p.Name).ShouldContain("DA9");
    }

    [Fact]
    public async Task SaveMergedAsync_addnew_disambiguates_a_name_that_collides_with_an_existing_project()
    {
        using var h = new ServiceTestHarness();
        var svc = new FinanceProjectService(new FinanceProjectRepository(h.Context));

        await svc.CreateAsync(new Project { Name = "DA1", ContractValue = 1_000m });
        h.Context.ChangeTracker.Clear();

        // Two brand-new rows both named "DA1" — the existing one owns "DA1", so
        // the batch must land as "DA1 (2)" and "DA1 (3)", never duplicating.
        var written = await svc.SaveMergedAsync(new[]
        {
            new ImportCandidate(new Project { Name = "DA1", ContractValue = 10m }, null, ImportResolution.AddNew),
            new ImportCandidate(new Project { Name = "DA1", ContractValue = 20m }, null, ImportResolution.AddNew)
        });
        h.Context.ChangeTracker.Clear();

        written.ShouldBe(2);
        var names = (await svc.ListAsync()).Select(p => p.Name).ToList();
        names.Count.ShouldBe(3);
        names.ShouldContain("DA1");
        names.ShouldContain("DA1 (2)");
        names.ShouldContain("DA1 (3)");
    }
}
