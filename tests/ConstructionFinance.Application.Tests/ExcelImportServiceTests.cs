using ClosedXML.Excel;
using ConstructionFinance.Application.Services.Implementations;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class ExcelImportServiceTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "H.xlsx");

    private static string HieuFixturePath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "Hieu.xlsx");

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
    public async Task ParseAsync_handles_Hieu_xlsx_template()
    {
        File.Exists(HieuFixturePath).ShouldBeTrue($"Fixture missing: {HieuFixturePath}");
        var svc = new ExcelImportService();
        var result = await svc.ParseAsync(HieuFixturePath);

        result.Summaries.Count.ShouldBe(5);
        result.Summaries.ShouldAllBe(s => s.SheetName.StartsWith("DA", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParseAsync_Hieu_xlsx_attributes_NSNN_and_DTNH_separately()
    {
        var svc = new ExcelImportService();
        var result = await svc.ParseAsync(HieuFixturePath);

        var hasNSNNAttribution = result.Summaries.Any(s => s.DisbursedNSNN > 0);
        hasNSNNAttribution.ShouldBeTrue();
    }

    [Fact]
    public async Task ParseAsync_H_xlsx_splits_NSNN_and_DTNH_from_TongHop_sheet()
    {
        var path = CreateHStyleWorkbookWithTongHopSplit();
        try
        {
            var svc = new ExcelImportService();
            var result = await svc.ParseAsync(path);

            var project = result.Summaries.ShouldHaveSingleItem();
            project.DisbursedNSNN.ShouldBe(100m);
            project.DisbursedDTNH.ShouldBe(50m);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>
    /// H.xlsx fixture has empty Tổng hợp data rows; build minimal H-layout workbook to verify funding split.
    /// </summary>
    private static string CreateHStyleWorkbookWithTongHopSplit()
    {
        var path = Path.Combine(Path.GetTempPath(), $"h-split-{Guid.NewGuid():N}.xlsx");
        using var wb = new XLWorkbook();

        var da = wb.AddWorksheet("DA1");
        da.Cell("A2").Value = "Tên dự án: Dự án thử nghiệm";
        da.Cell("A6").Value = "TỔNG CỘNG";
        da.Cell("D6").Value = 1000m;
        da.Cell("F6").Value = 100m;
        da.Cell("G6").Value = 200m;
        da.Cell("H6").Value = 50m;
        da.Cell("I6").Value = 150m;
        da.Cell("J6").Value = 10m;
        da.Cell("K6").Value = 150m;

        var th = wb.AddWorksheet("Tổng hợp");
        th.Cell(9, 2).Value = "Dự án thử nghiệm";
        th.Cell(9, 10).Value = 100m;
        th.Cell(9, 11).Value = 50m;

        wb.SaveAs(path);
        return path;
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
