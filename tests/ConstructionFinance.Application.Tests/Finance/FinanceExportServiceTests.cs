using ClosedXML.Excel;
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Finance;

public class FinanceExportServiceTests
{
    private static Project SampleProject()
    {
        var p = new Project { Name = "DA1", ContractValue = 2_000_000_000m, ContractAnnexValue = 2_200_000_000m, Notes = "Hợp đồng số 01" };
        p.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 1, 5), PackageName = "Đợt ứng", Advance = 500_000_000m, DisbursedNSNN = 500_000_000m });
        p.Lines.Add(new ProjectLineItem { SortOrder = 2, Date = new DateOnly(2026, 2, 1), PaymentKLHT = 800_000_000m, RecoveredAdvance = 200_000_000m, DisbursedNSNN = 420_000_000m, DisbursedNgoaiHoi = 180_000_000m });
        return p;
    }

    [Fact]
    public async Task ExportAsync_writes_a_DA_sheet_with_dot_values_and_totals()
    {
        var target = Path.Combine(Path.GetTempPath(), $"fin-exp-{Guid.NewGuid():N}.xlsx");
        try
        {
            await new FinanceExportService().ExportAsync(new[] { SampleProject() }, target);

            using var wb = new XLWorkbook(target);
            var da = wb.Worksheet("DA1");

            // contract row (row 3): C = ContractValue, D = ContractAnnexValue
            da.Cell(3, 3).GetValue<decimal>().ShouldBe(2_000_000_000m);
            da.Cell(3, 4).GetValue<decimal>().ShouldBe(2_200_000_000m);

            // đợt row 1 (row 4): E Advance, H NSNN, J Giải ngân (= H + I)
            da.Cell(4, 5).GetValue<decimal>().ShouldBe(500_000_000m);
            da.Cell(4, 8).GetValue<decimal>().ShouldBe(500_000_000m);
            da.Cell(4, 10).GetValue<decimal>().ShouldBe(500_000_000m);

            // đợt row 2 (row 5): J Giải ngân = 420M + 180M = 600M
            da.Cell(5, 10).GetValue<decimal>().ShouldBe(600_000_000m);

            // TỔNG CỘNG row (row 6): E Σadvance=500M, H Σnsnn=920M, J TotalDisbursed=1_100M
            da.Cell(6, 1).GetString().ShouldContain("TỔNG");
            da.Cell(6, 5).GetValue<decimal>().ShouldBe(500_000_000m);
            da.Cell(6, 8).GetValue<decimal>().ShouldBe(920_000_000m);
            da.Cell(6, 10).GetValue<decimal>().ShouldBe(1_100_000_000m);
        }
        finally { if (File.Exists(target)) File.Delete(target); }
    }

    [Fact]
    public async Task ExportAsync_writes_a_TH_summary_with_per_project_rows_and_grand_total()
    {
        var p2 = new Project { Name = "DA2", ContractValue = 1_000_000_000m, ContractorName = "Nhà thầu B" };
        p2.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 3, 1), DisbursedNSNN = 300_000_000m });

        var target = Path.Combine(Path.GetTempPath(), $"fin-exp-th-{Guid.NewGuid():N}.xlsx");
        try
        {
            await new FinanceExportService().ExportAsync(new[] { SampleProject(), p2 }, target);

            using var wb = new XLWorkbook(target);
            var th = wb.Worksheet("TH");
            th.Position.ShouldBe(1); // TH is the first sheet

            // header row 2, project rows from row 3
            th.Cell(3, 2).GetString().ShouldBe("DA1");
            th.Cell(3, 10).GetValue<decimal>().ShouldBe(1_100_000_000m); // DA1 TotalDisbursed
            th.Cell(4, 2).GetString().ShouldBe("DA2");
            th.Cell(4, 3).GetString().ShouldBe("Nhà thầu B");
            th.Cell(4, 10).GetValue<decimal>().ShouldBe(300_000_000m);

            // grand-total row (row 5): J = 1_100M + 300M = 1_400M
            th.Cell(5, 1).GetString().ShouldContain("TỔNG");
            th.Cell(5, 10).GetValue<decimal>().ShouldBe(1_400_000_000m);
        }
        finally { if (File.Exists(target)) File.Delete(target); }
    }
}
