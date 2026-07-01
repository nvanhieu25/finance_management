using ClosedXML.Excel;
using ConstructionFinance.Application.Services.Implementations;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Finance;

public class FinanceImportServiceTests
{
    private static string HieuFixture => Path.Combine(AppContext.BaseDirectory, "TestData", "Hieu.xlsx");

    // Builds one DA sheet in the legacy layout: total row 4, contract row 5, two đợt (rows 6-7).
    private static string BuildDaWorkbook()
    {
        var path = Path.Combine(Path.GetTempPath(), $"da-{Guid.NewGuid():N}.xlsx");
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("DA1");
        ws.Cell("A2").Value = "Ngày tháng"; ws.Cell("B2").Value = "Tên gói thầu";
        ws.Cell("C2").Value = "Hợp đồng"; ws.Cell("D2").Value = "Phụ lục HĐ";
        ws.Cell("B4").Value = "Tổng cộng:";
        // contract row (row 5): C/D set, no đợt amounts
        ws.Cell("A5").Value = new DateTime(2026, 1, 1);
        ws.Cell("B5").Value = "Hợp đồng số 01/2026";
        ws.Cell("C5").Value = 2_000_000_000d;
        ws.Cell("D5").Value = 2_200_000_000d;
        // đợt 1 (row 6): advance
        ws.Cell("A6").Value = new DateTime(2026, 1, 5);
        ws.Cell("B6").Value = "Đợt ứng";
        ws.Cell("E6").Value = 500_000_000d;
        ws.Cell("H6").Value = 500_000_000d;
        // đợt 2 (row 7): KLHT payment
        ws.Cell("A7").Value = new DateTime(2026, 2, 1);
        ws.Cell("F7").Value = 800_000_000d;
        ws.Cell("G7").Value = 200_000_000d;
        ws.Cell("H7").Value = 600_000_000d;
        ws.Cell("I7").Value = 100_000_000d;
        wb.SaveAs(path);
        return path;
    }

    [Fact]
    public async Task ParseWorkbook_maps_contract_row_and_dot_rows()
    {
        var path = BuildDaWorkbook();
        try
        {
            var result = await new FinanceImportService().ParseWorkbookAsync(path);

            var p = result.Projects.ShouldHaveSingleItem();
            p.Name.ShouldBe("DA1");
            p.ContractValue.ShouldBe(2_000_000_000m);
            p.ContractAnnexValue.ShouldBe(2_200_000_000m);

            p.Lines.Count.ShouldBe(2);
            var l1 = p.Lines[0];
            l1.SortOrder.ShouldBe(1);
            l1.Date.ShouldBe(new DateOnly(2026, 1, 5));
            l1.Advance.ShouldBe(500_000_000m);
            l1.DisbursedNSNN.ShouldBe(500_000_000m);
            var l2 = p.Lines[1];
            l2.PaymentKLHT.ShouldBe(800_000_000m);
            l2.RecoveredAdvance.ShouldBe(200_000_000m);
            l2.DisbursedNSNN.ShouldBe(600_000_000m);
            l2.DisbursedNgoaiHoi.ShouldBe(100_000_000m);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task ParseWorkbook_reads_cached_formula_values_from_real_sample()
    {
        File.Exists(HieuFixture).ShouldBeTrue($"Fixture missing: {HieuFixture}");
        var result = await new FinanceImportService().ParseWorkbookAsync(HieuFixture);

        result.Projects.Count.ShouldBe(5); // DA1..DA5
        var da1 = result.Projects.Single(p => p.Name == "DA1");
        da1.ContractValue.ShouldBe(1_000_000_000m);
        da1.ContractAnnexValue.ShouldBe(1_100_000_000m);
        // row 6: Advance 500M, and H6 formula '=E6' → cached value 500M read as DisbursedNSNN
        da1.Lines.ShouldContain(l => l.Advance == 500_000_000m && l.DisbursedNSNN == 500_000_000m);
    }
}
