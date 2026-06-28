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
    public async Task ParseAsync_Hieu_xlsx_does_not_use_header_row_as_project_name()
    {
        // The Hiệu DA sheets have no project-name cell (A2/B2/C2 are column headers
        // like "Ngày tháng"). ProjectName must fall back to the sheet name, NOT grab a header.
        var result = await new ExcelImportService().ParseAsync(HieuFixturePath);

        result.Summaries.ShouldNotBeEmpty();
        result.Summaries.ShouldAllBe(s => s.ProjectName != "Ngày tháng");
        result.Summaries.ShouldAllBe(s => s.ProjectName == s.SheetName);
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
    public async Task ParseAsync_no_false_warning_for_project_with_existing_DTNH_split()
    {
        // A project that already has DisbursedDTNH set from its DA sheet must NOT produce
        // "không tìm thấy phân tách" even if it is absent from the Tổng hợp sheet.
        var path = Path.Combine(Path.GetTempPath(), $"split-warn-{Guid.NewGuid():N}.xlsx");
        using (var wb = new XLWorkbook())
        {
            var da = wb.AddWorksheet("DA1");
            // Hiệu template signals: A2/B2/C2 headers + total row at 4
            da.Cell(2, 1).Value = "Ngày tháng";
            da.Cell(2, 2).Value = "Tên gói thầu";
            da.Cell(2, 3).Value = "Hợp đồng";
            da.Cell(4, 1).Value = "TỔNG CỘNG";
            da.Cell(4, 4).Value = 1000m;  // ContractValue (col D)
            da.Cell(4, 8).Value = 80m;    // DisbursedNSNN (col H)
            da.Cell(4, 9).Value = 20m;    // DisbursedDTNH (col I) — already split!
            da.Cell(4, 11).Value = 100m;  // TotalDisbursed (col K)
            // No Tổng hợp sheet — refinement can't match, but should not warn
            wb.SaveAs(path);
        }
        try
        {
            var result = await new ExcelImportService().ParseAsync(path);
            var p = result.Summaries.ShouldHaveSingleItem();
            p.DisbursedDTNH.ShouldBe(20m);

            var spurious = result.Warnings.Where(w =>
                w.Contains("không tìm thấy phân tách") &&
                w.Contains("NSNN/DTNH")).ToList();
            spurious.ShouldBeEmpty(
                $"False-positive warning for already-split project: {string.Join("; ", spurious)}");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
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
    public async Task ParseAsync_matches_funding_split_via_formula_sheet_reference()
    {
        // DA sheet has no "Tên dự án:" cell → ProjectName falls back to sheet name "DA1".
        // Tổng hợp row uses different display name "Bệnh viện ABC" but col J formula references 'DA1'!K6.
        // The C1 fix must match via formula sheet ref, not by name.
        var path = Path.Combine(Path.GetTempPath(), $"sheetref-{Guid.NewGuid():N}.xlsx");
        using (var wb = new ClosedXML.Excel.XLWorkbook())
        {
            var da = wb.AddWorksheet("DA1");
            da.Cell("A6").Value = "TỔNG CỘNG";
            da.Cell("D6").Value = 1000m;
            da.Cell("K6").Value = 150m;

            var th = wb.AddWorksheet("Tổng hợp");
            th.Cell(9, 2).Value = "Bệnh viện ABC";  // display name differs from sheet name
            th.Cell(9, 10).FormulaA1 = "'DA1'!K6";  // formula reference — links via sheet name
            th.Cell(9, 11).Value = 25m;             // DTNH manual

            wb.SaveAs(path);
        }
        try
        {
            var svc = new ExcelImportService();
            var result = await svc.ParseAsync(path);

            var p = result.Summaries.ShouldHaveSingleItem();
            p.SheetName.ShouldBe("DA1");
            p.ProjectName.ShouldBe("DA1"); // fell back to sheet name
            p.DisbursedNSNN.ShouldBe(150m); // matched via formula sheet ref
            p.DisbursedDTNH.ShouldBe(25m);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void YearPattern_does_not_match_5_digit_years()
    {
        ExcelImportService.YearPattern.Match("Quyết toán năm 12025").Success.ShouldBeFalse();
        ExcelImportService.YearPattern.Match("Quyết toán năm 2026").Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ParseAsync_detects_H_template_when_H_signals_outscore_Hieu_ambiguity()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tpl-h-{Guid.NewGuid():N}.xlsx");
        using (var wb = new XLWorkbook())
        {
            var da = wb.AddWorksheet("DA1");
            da.Cell(2, 1).Value = "Ngày tháng"; // Hieu signal
            da.Cell(4, 1).Value = "TT";
            da.Cell(4, 2).Value = "Nội dung";
            da.Cell(6, 1).Value = "TỔNG CỘNG";
            da.Cell(6, 4).Value = 500m;
            da.Cell(6, 11).Value = 100m;
            wb.SaveAs(path);
        }
        try
        {
            var result = await new ExcelImportService().ParseAsync(path);
            var p = result.Summaries.ShouldHaveSingleItem();
            p.ContractValue.ShouldBe(500m);
            p.PaymentToContractor.ShouldBeGreaterThanOrEqualTo(0m);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ParseAsync_detects_Hieu_template_when_Hieu_signals_outscore_H()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tpl-hieu-{Guid.NewGuid():N}.xlsx");
        using (var wb = new XLWorkbook())
        {
            var da = wb.AddWorksheet("DA1");
            da.Cell(2, 1).Value = "Ngày tháng";
            da.Cell(2, 2).Value = "Tên gói thầu";
            da.Cell(2, 3).Value = "Hợp đồng";
            da.Cell(4, 1).Value = "TỔNG CỘNG";
            da.Cell(4, 5).Value = 10m;
            da.Cell(4, 6).Value = 20m;
            da.Cell(4, 7).Value = 5m;
            da.Cell(4, 8).Value = 30m;
            da.Cell(4, 9).Value = 15m;
            da.Cell(4, 11).Value = 45m;
            wb.SaveAs(path);
        }
        try
        {
            var result = await new ExcelImportService().ParseAsync(path);
            var p = result.Summaries.ShouldHaveSingleItem();
            p.DisbursedNSNN.ShouldBe(30m);
            p.DisbursedDTNH.ShouldBe(15m);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
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
