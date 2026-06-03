using ConstructionFinance.Application.Services.Implementations;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class ExcelImportServiceTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "H.xlsx");

    [Fact]
    public async Task ParseAsync_finds_DA_sheets_from_H_xlsx()
    {
        File.Exists(FixturePath).ShouldBeTrue($"Fixture missing: {FixturePath}");
        var svc = new ExcelImportService();
        var result = await svc.ParseAsync(FixturePath);

        result.Summaries.Count.ShouldBeGreaterThan(0);
        result.Summaries.ShouldAllBe(s => s.SheetName.StartsWith("DA", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParseAsync_collects_warnings_for_ref_errors()
    {
        var svc = new ExcelImportService();
        var result = await svc.ParseAsync(FixturePath);

        result.Warnings.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ParseAsync_detects_year_columns_dynamically()
    {
        var svc = new ExcelImportService();
        var result = await svc.ParseAsync(FixturePath);

        var years = result.Summaries.SelectMany(s => s.YearlySettlements).Select(y => y.Year).Distinct().ToList();
        years.ShouldNotBeEmpty();
        years.ShouldAllBe(y => y >= 2020 && y <= 2035);
    }

    [Fact]
    public async Task ParseAsync_throws_when_file_missing()
    {
        var svc = new ExcelImportService();
        await Should.ThrowAsync<FileNotFoundException>(() => svc.ParseAsync("/tmp/nonexistent.xlsx"));
    }

    [Fact]
    public async Task ExportTongHopAsync_creates_target_file_with_TH_sheet()
    {
        var importer = new ExcelImportService();
        var exporter = new ExcelExportService();
        var parsed = await importer.ParseAsync(FixturePath);

        var target = Path.Combine(Path.GetTempPath(), $"export-{Guid.NewGuid():N}.xlsx");
        try
        {
            var result = await exporter.ExportTongHopAsync(FixturePath, target, parsed.Summaries);
            File.Exists(result).ShouldBeTrue();
            using var wb = new ClosedXML.Excel.XLWorkbook(result);
            wb.Worksheets.Any(w =>
                w.Name.Equals("Tổng hợp", StringComparison.OrdinalIgnoreCase) ||
                w.Name.Equals("TH", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
        }
        finally
        {
            if (File.Exists(target)) File.Delete(target);
        }
    }
}
