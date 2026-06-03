using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Data;
using ConstructionFinance.Data.Repositories;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class ExcelImportRoundTripTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "H.xlsx");

    [Fact]
    public async Task RoundTrip_parses_H_xlsx_persists_and_reloads_with_matching_values()
    {
        var importer = new ExcelImportService();
        var parsed = await importer.ParseAsync(FixturePath);
        parsed.Summaries.Count.ShouldBeGreaterThan(0);

        using var h = new ServiceTestHarness();
        var svc = new ProjectSummaryService(new ProjectSummaryRepository(h.Context), new UnitOfWork(h.Context));
        var batchId = await svc.SaveBatchAsync(parsed);

        var reloaded = await svc.ListByBatchAsync(batchId);
        reloaded.Count.ShouldBe(parsed.Summaries.Count);
        reloaded[0].SheetName.ShouldBe(parsed.Summaries[0].SheetName);
        reloaded[0].TotalDisbursed.ShouldBe(parsed.Summaries[0].TotalDisbursed);
    }

    [Fact]
    public async Task RoundTrip_export_after_import_produces_valid_xlsx()
    {
        var importer = new ExcelImportService();
        var exporter = new ExcelExportService();
        var parsed = await importer.ParseAsync(FixturePath);

        using var h = new ServiceTestHarness();
        var svc = new ProjectSummaryService(new ProjectSummaryRepository(h.Context), new UnitOfWork(h.Context));
        await svc.SaveBatchAsync(parsed);

        var target = Path.Combine(Path.GetTempPath(), $"rt-{Guid.NewGuid():N}.xlsx");
        try
        {
            await exporter.ExportTongHopAsync(FixturePath, target, parsed.Summaries);
            File.Exists(target).ShouldBeTrue();
            new FileInfo(target).Length.ShouldBeGreaterThan(1000);
        }
        finally
        {
            if (File.Exists(target)) File.Delete(target);
        }
    }
}
