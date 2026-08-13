using ClosedXML.Excel;
using ConstructionFinance.Application.Forms;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

public class FormExcelExporterKsChiGdnttTests
{
    private static string TempPath(string prefix) =>
        Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}.xlsx");

    private static KsChiForm SampleKsChi() => new(
        NgayNhanGiayDeNghi: "05/03/2026",
        MucTieuMucLines: new[] { "Mục 9300, tiểu mục 9301", "", "", "" },
        Accepted: new KsChiAmounts(240_000_000m, 210_000_000m, 30_000_000m),
        Recovered: new KsChiAmounts(5_000_000m, 5_000_000m, 0m),
        ThuHoiCacNamTruoc: 1_000_000m,
        ThuHoiNamNay: 4_000_000m,
        ThueGTGT: 2_000_000m,
        PaidToBeneficiary: new KsChiAmounts(235_000_000m, 205_000_000m, 30_000_000m),
        PaidInWords: "Hai trăm ba mươi lăm triệu đồng.",
        SoTuChoi: 500_000m,
        LyDoTuChoi: "thiếu hồ sơ",
        GhiChu: "Tạm ứng lần 1");

    private static GdnttForm SampleGdntt() => new(
        DonViCapTren: "Sở Tài chính",
        DonViCoSo: "Ban QLDA số 1",
        So: "07/GĐN",
        NgayThangNam: "05/03/2026",
        KinhGui: "KBNN Hải Phòng",
        TenDuAnCongTrinh: "Dự án A",
        ChuDauTu: "Ban QLDA số 1",
        CanCuQuyetDinh: "QĐ 91/TTg",
        CanCuHopDong: "HĐ 01/2024",
        CanCuGiayDeNghi: "GĐN 03",
        ThuocKeHoach: "Kế hoạch 2026",
        PackageName: "Gói A",
        ContractorName: "Cty XL A",
        BankAccount: "123456789",
        TaiNganHang: "Vietcombank Hải Phòng",
        UnrecoveredAdvanceBefore: 6_000_000m,
        ContractTotal: 52_000_000_000m,
        CumulativeBefore: 210_000_000m,
        RequestedThisPeriod: 30_000_000m,
        RequestedInWords: "Ba mươi triệu đồng.",
        RecoveredThisPeriod: 0m,
        ChuyenTienBaoHanh: 2_000_000m,
        ChuyenTienTamGiu: 1_000_000m,
        PaidToContractor: 27_000_000m,
        NguonVon: "Quỹ dự trữ ngoại hối");

    [Fact]
    public async Task ExportKsChiAsync_writes_the_three_fund_columns()
    {
        var path = TempPath("ks");
        try
        {
            await new FormExcelExporter().ExportKsChiAsync(SampleKsChi(), path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("KS chi");

            ws.Cell(1, 1).GetString().ShouldBe("PHẦN GHI CỦA CƠ QUAN KIỂM SOÁT, THANH TOÁN");
            ws.Cell(3, 4).GetString().ShouldContain("05/03/2026");
            ws.Cell(6, 3).GetString().ShouldBe("Tổng số");
            ws.Cell(6, 4).GetString().ShouldBe("Vốn trong nước");
            ws.Cell(6, 5).GetString().ShouldBe("Vốn nước ngoài");

            ws.Cell(7, 1).GetString().ShouldBe("Số vốn chấp nhận");
            ws.Cell(7, 3).GetValue<decimal>().ShouldBe(240_000_000m);
            ws.Cell(7, 4).GetValue<decimal>().ShouldBe(210_000_000m);
            ws.Cell(7, 5).GetValue<decimal>().ShouldBe(30_000_000m);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExportKsChiAsync_writes_recovery_tax_and_beneficiary_rows()
    {
        var path = TempPath("ks");
        try
        {
            await new FormExcelExporter().ExportKsChiAsync(SampleKsChi(), path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("KS chi");

            ws.Cell(8, 1).GetString().ShouldContain("Mục 9300, tiểu mục 9301");
            ws.Cell(13, 3).GetValue<decimal>().ShouldBe(5_000_000m);      // Số thu hồi tạm ứng
            ws.Cell(14, 3).GetValue<decimal>().ShouldBe(1_000_000m);      // Các năm trước
            ws.Cell(15, 3).GetValue<decimal>().ShouldBe(4_000_000m);      // Năm nay
            ws.Cell(16, 3).GetValue<decimal>().ShouldBe(2_000_000m);      // Thuế GTGT
            ws.Cell(17, 1).GetString().ShouldContain("Số trả đơn vị thụ hưởng");
            ws.Cell(17, 3).GetValue<decimal>().ShouldBe(235_000_000m);
            ws.Cell(18, 1).GetString().ShouldBe("Hai trăm ba mươi lăm triệu đồng.");
            ws.Cell(19, 3).GetValue<decimal>().ShouldBe(500_000m);        // Số từ chối
            ws.Cell(20, 1).GetString().ShouldContain("thiếu hồ sơ");
            ws.Cell(22, 2).GetString().ShouldBe("Tạm ứng lần 1");         // Ghi chú
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExportGdnttAsync_writes_the_header_and_basis_lines()
    {
        var path = TempPath("gd");
        try
        {
            await new FormExcelExporter().ExportGdnttAsync(SampleGdntt(), path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("GĐNTT");

            ws.Cell(1, 1).GetString().ShouldBe("Sở Tài chính");
            ws.Cell(1, 4).GetString().ShouldBe("CỘNG HOÀ XÃ HỘI CHỦ NGHĨA VIỆT NAM");
            ws.Cell(2, 4).GetString().ShouldBe("Độc lập - Tự do - Hạnh phúc");
            ws.Cell(3, 1).GetString().ShouldContain("07/GĐN");
            ws.Cell(4, 1).GetString().ShouldBe("GIẤY ĐỀ NGHỊ THANH TOÁN VỐN ĐẦU TƯ");
            ws.Cell(6, 1).GetString().ShouldContain("KBNN Hải Phòng");
            ws.Cell(10, 1).GetString().ShouldContain("QĐ 91/TTg");
            ws.Cell(11, 1).GetString().ShouldContain("HĐ 01/2024");
            ws.Cell(15, 1).GetString().ShouldContain("Quỹ dự trữ ngoại hối");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExportGdnttAsync_writes_the_amount_table_and_beneficiary_block()
    {
        var path = TempPath("gd");
        try
        {
            await new FormExcelExporter().ExportGdnttAsync(SampleGdntt(), path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("GĐNTT");

            ws.Cell(13, 4).GetValue<decimal>().ShouldBe(6_000_000m);       // Số dư tạm ứng
            ws.Cell(18, 1).GetString().ShouldBe("Gói A");
            ws.Cell(18, 4).GetValue<decimal>().ShouldBe(52_000_000_000m);  // Giá trị HĐ + PLHĐ
            ws.Cell(18, 6).GetValue<decimal>().ShouldBe(210_000_000m);     // Lũy kế kỳ trước
            ws.Cell(18, 8).GetValue<decimal>().ShouldBe(30_000_000m);      // Kỳ này
            ws.Cell(19, 1).GetString().ShouldBe("CỘNG");
            ws.Cell(20, 8).GetValue<decimal>().ShouldBe(30_000_000m);
            ws.Cell(21, 2).GetString().ShouldBe("Ba mươi triệu đồng.");
            ws.Cell(23, 8).GetValue<decimal>().ShouldBe(0m);               // thu hồi tạm ứng
            ws.Cell(24, 8).GetValue<decimal>().ShouldBe(2_000_000m);       // bảo hành
            ws.Cell(25, 8).GetValue<decimal>().ShouldBe(1_000_000m);       // tạm giữ
            ws.Cell(26, 8).GetValue<decimal>().ShouldBe(27_000_000m);      // trả nhà thầu
            ws.Cell(27, 3).GetString().ShouldBe("Cty XL A");
            ws.Cell(28, 4).GetString().ShouldBe("123456789");
            ws.Cell(29, 2).GetString().ShouldBe("Vietcombank Hải Phòng");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExportGdnttAsync_merges_the_cong_row_the_same_way_as_the_package_row_above()
    {
        var path = TempPath("gd");
        try
        {
            await new FormExcelExporter().ExportGdnttAsync(SampleGdntt(), path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("GĐNTT");

            // Row 19 (CỘNG) must carry its values AND merge 4:5 / 6:7 / 8:9 exactly like
            // row 18 (the package row) above it — otherwise the columns don't line up
            // inside the shared border block.
            ws.Cell(19, 4).GetValue<decimal>().ShouldBe(52_000_000_000m);
            ws.Cell(19, 6).GetValue<decimal>().ShouldBe(210_000_000m);
            ws.Cell(19, 8).GetValue<decimal>().ShouldBe(30_000_000m);

            ws.Cell(19, 4).MergedRange()!.LastColumn().ColumnNumber().ShouldBe(5);
            ws.Cell(19, 6).MergedRange()!.LastColumn().ColumnNumber().ShouldBe(7);
            ws.Cell(19, 8).MergedRange()!.LastColumn().ColumnNumber().ShouldBe(9);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExportGdnttAsync_prints_landscape_a4()
    {
        var path = TempPath("gd");
        try
        {
            await new FormExcelExporter().ExportGdnttAsync(SampleGdntt(), path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("GĐNTT");
            ws.PageSetup.PageOrientation.ShouldBe(XLPageOrientation.Landscape);
        }
        finally { File.Delete(path); }
    }
}
