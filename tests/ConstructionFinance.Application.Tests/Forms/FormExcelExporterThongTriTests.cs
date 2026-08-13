using ClosedXML.Excel;
using ConstructionFinance.Application.Forms;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

public class FormExcelExporterThongTriTests
{
    private static ThongTriForm Sample() => new(
        DonViCapTren: "Sở Tài chính",
        DonViCoSo: "Ban QLDA số 1",
        SoThongTri: "12/TT",
        NgayGhiSo: "05/03/2026",
        TenCTMT: "CTMT X",
        MaSo: "A1",
        VeThangNam: "tháng 3 năm 2026",
        TaiKhoan1: "TK1", DonVi1: "ĐV1", SoChungTu1: "CT1",
        TaiKhoan2: "TK2", DonVi2: "ĐV2", SoChungTu2: "CT2",
        LoaiNganSach: "10", Khoan: "11", Muc: "9300", TieuMuc: "9301", TietMuc: "0", Nganh: "0",
        Lines: new ThongTriLine[]
        {
            new(0, "Dự án A", 35_000_000m),
            new(1, "Vị trí 1", 15_000_000m),
            new(2, "Tạm ứng lần 1", 10_000_000m),
            new(2, "Tạm ứng lần 2", 5_000_000m),
            new(1, "Vị trí 2", 20_000_000m),
            new(2, "Tạm ứng lần 1", 20_000_000m)
        },
        Total: 35_000_000m,
        TotalInWords: "Ba mươi lăm triệu đồng.");

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"tt-{Guid.NewGuid():N}.xlsx");

    [Fact]
    public async Task ExportThongTriAsync_writes_the_header_block()
    {
        var path = TempPath();
        try
        {
            await new FormExcelExporter().ExportThongTriAsync(Sample(), path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("Thông tri");
            ws.Cell(1, 1).GetString().ShouldBe("Sở Tài chính");
            ws.Cell(1, 7).GetString().ShouldBe("Mẫu số CQ06-HD");
            ws.Cell(2, 1).GetString().ShouldBe("Ban QLDA số 1");
            ws.Cell(3, 1).GetString().ShouldContain("12/TT");
            ws.Cell(3, 7).GetString().ShouldContain("05/03/2026");
            ws.Cell(4, 1).GetString().ShouldBe("THÔNG TRI");
            ws.Cell(9, 1).GetString().ShouldContain("tháng 3 năm 2026");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExportThongTriAsync_writes_the_budget_index_row()
    {
        var path = TempPath();
        try
        {
            await new FormExcelExporter().ExportThongTriAsync(Sample(), path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("Thông tri");
            ws.Cell(15, 1).GetString().ShouldBe("10");     // Loại ngân sách
            ws.Cell(15, 2).GetString().ShouldBe("11");     // Khoản
            ws.Cell(15, 3).GetString().ShouldBe("9300");   // Mục
            ws.Cell(15, 4).GetString().ShouldBe("9301");   // Tiểu mục
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExportThongTriAsync_writes_one_row_per_line_indented_by_level()
    {
        var path = TempPath();
        try
        {
            await new FormExcelExporter().ExportThongTriAsync(Sample(), path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("Thông tri");

            ws.Cell(15, 7).GetString().ShouldBe("Dự án A");
            ws.Cell(15, 8).GetValue<decimal>().ShouldBe(35_000_000m);
            ws.Cell(16, 7).GetString().ShouldBe("Vị trí 1");
            ws.Cell(17, 7).GetString().ShouldBe("Tạm ứng lần 1");
            ws.Cell(17, 8).GetValue<decimal>().ShouldBe(10_000_000m);
            ws.Cell(20, 7).GetString().ShouldBe("Tạm ứng lần 1");
            ws.Cell(20, 8).GetValue<decimal>().ShouldBe(20_000_000m);

            // Dòng Cộng ngay sau dòng cuối, rồi dòng Bằng chữ.
            ws.Cell(21, 7).GetString().ShouldBe("Cộng");
            ws.Cell(21, 8).GetValue<decimal>().ShouldBe(35_000_000m);
            ws.Cell(22, 1).GetString().ShouldBe("Bằng chữ: Ba mươi lăm triệu đồng.");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExportThongTriAsync_formats_money_with_thousands_separators()
    {
        var path = TempPath();
        try
        {
            await new FormExcelExporter().ExportThongTriAsync(Sample(), path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("Thông tri");
            ws.Cell(15, 8).Style.NumberFormat.Format.ShouldBe("#,##0");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExportThongTriAsync_survives_an_empty_selection()
    {
        var path = TempPath();
        var empty = Sample() with
        {
            Lines = new ThongTriLine[] { new(0, "Dự án A", 0m) },
            Total = 0m,
            TotalInWords = "Không đồng."
        };
        try
        {
            await new FormExcelExporter().ExportThongTriAsync(empty, path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("Thông tri");
            ws.Cell(16, 7).GetString().ShouldBe("Cộng");
            ws.Cell(17, 1).GetString().ShouldBe("Bằng chữ: Không đồng.");
        }
        finally { File.Delete(path); }
    }
}
