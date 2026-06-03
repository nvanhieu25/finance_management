using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Data;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class ProjectSummaryServiceTests
{
    private static ProjectSummaryService CreateService(ServiceTestHarness h) =>
        new(new ProjectSummaryRepository(h.Context), new UnitOfWork(h.Context));

    private static ImportResultDto SampleImport() => new(
        "H.xlsx",
        "abc123",
        6,
        new[]
        {
            new ProjectSummaryDto(
                "DA1", "Dự án A", "NT A",
                1_000m, 100m, 200m, 50m, 150m, 50m, 800m, 0m, 800m,
                new[] { new SettlementByYearDto(2025, 100m, 20m, 30m, 10m) })
        },
        Array.Empty<string>());

    [Fact]
    public async Task SaveBatchAsync_persists_batch_with_summaries_and_yearlySettlements()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h);

        var id = await svc.SaveBatchAsync(SampleImport());

        id.ShouldBeGreaterThan(0);
        var list = await svc.ListByBatchAsync(id);
        list.Count.ShouldBe(1);
        list[0].YearlySettlements.Count.ShouldBe(1);
        list[0].YearlySettlements[0].Year.ShouldBe(2025);
    }

    [Fact]
    public async Task GetLatestBatchAsync_returns_most_recent_by_ImportedAtUtc()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h);
        await svc.SaveBatchAsync(SampleImport());
        await Task.Delay(5);
        var second = SampleImport() with { SourceFileName = "H2.xlsx" };
        var id2 = await svc.SaveBatchAsync(second);

        var latest = await svc.GetLatestBatchAsync();
        latest.ShouldNotBeNull();
        latest!.Id.ShouldBe(id2);
    }

    [Fact]
    public async Task AggregateAsync_sums_columns_across_summaries()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h);
        var batchId = await svc.SaveBatchAsync(SampleImport() with
        {
            Summaries = new[]
            {
                SampleImport().Summaries[0],
                SampleImport().Summaries[0] with { SheetName = "DA2", ProjectName = "B", ContractValue = 500m, TotalDisbursed = 400m }
            }
        });

        var agg = await svc.AggregateAsync(batchId);
        agg.ProjectCount.ShouldBe(2);
        agg.ContractValue.ShouldBe(1_500m);
        agg.TotalDisbursed.ShouldBe(1_200m);
    }
}
