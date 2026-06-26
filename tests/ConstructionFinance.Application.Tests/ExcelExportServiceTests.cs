using ClosedXML.Excel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Implementations;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class ExcelExportServiceTests
{
    private static string HFixture => Path.Combine(AppContext.BaseDirectory, "TestData", "H.xlsx");

    private static ProjectSummaryDto SummaryWithYears(IReadOnlyList<SettlementByYearDto> years) => new(
        "DA1", "Dự án X", "NT", 1000m, 0, 0, 0, 0, 0, 0, 0, 0, years);

    [Fact]
    public async Task ExportTongHopAsync_H_template_places_year_settlement_in_matching_year_column()
    {
        // A project that only has a 2026 settlement must NOT land its numbers in the
        // physical 2025 columns (M-O). Regression for the old "by list index" mapping.
        File.Exists(HFixture).ShouldBeTrue($"Fixture missing: {HFixture}");
        var exporter = new ExcelExportService();
        var summary = SummaryWithYears(new[]
        {
            new SettlementByYearDto(2026, KLHT: null, Advance: null, NSNN: 500m, DTNH: 200m)
        });

        var target = Path.Combine(Path.GetTempPath(), $"exp-{Guid.NewGuid():N}.xlsx");
        try
        {
            await exporter.ExportTongHopAsync(HFixture, target, new[] { summary });

            using var wb = new XLWorkbook(target);
            var sheet = wb.Worksheets.First(w =>
                w.Name.Equals("Tổng hợp", StringComparison.OrdinalIgnoreCase) ||
                w.Name.Equals("TH", StringComparison.OrdinalIgnoreCase));

            // 2025 columns (M-O = 13..15) must be empty for a 2026-only project.
            sheet.Cell(9, 13).IsEmpty().ShouldBeTrue("col 13 (2025 KLHT) should be empty");
            sheet.Cell(9, 14).IsEmpty().ShouldBeTrue("col 14 (2025 Advance) should be empty");
            sheet.Cell(9, 15).IsEmpty().ShouldBeTrue("col 15 (2025 DTNH) should be empty");
            // 2026 columns (P-Q = 16..17) must hold the NSNN/DTNH values.
            sheet.Cell(9, 16).GetValue<decimal>().ShouldBe(500m);
            sheet.Cell(9, 17).GetValue<decimal>().ShouldBe(200m);
        }
        finally
        {
            if (File.Exists(target)) File.Delete(target);
        }
    }
}
