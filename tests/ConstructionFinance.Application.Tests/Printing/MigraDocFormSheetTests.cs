using ClosedXML.Excel;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Printing;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// Ba biểu mẫu dựng bằng MigraDoc. Nghiệm thu chính: so từng ô với bản .xlsx của cùng dữ liệu
/// (SheetTextMap) — chép sai một ô là đỏ, và thông báo nói rõ ô nào.
public class MigraDocFormSheetTests
{
    private static readonly PrintFooterInfo Footer = new("Dự án A", null);

    // ===== Thông tri =====

    private static ThongTriForm ThongTriFixture() => new(
        DonViCapTren: "SỞ XÂY DỰNG",
        DonViCoSo: "Ban QLDA số 1",
        SoThongTri: "42/TT",
        NgayGhiSo: "07/10/2026",
        TenCTMT: "Đường ven biển",
        MaSo: "DA-01",
        VeThangNam: "tháng 10 năm 2026",
        TaiKhoan1: "3741",
        DonVi1: "BQL",
        SoChungTu1: "CT-01",
        TaiKhoan2: "3742",
        DonVi2: "BQL",
        SoChungTu2: "CT-02",
        LoaiNganSach: "NSNN",
        Khoan: "012",
        Muc: "9050",
        TieuMuc: "9062",
        TietMuc: "01",
        Nganh: "280",
        Lines: new ThongTriLine[]
        {
            new(0, "Đường ven biển", 50_000_000_000m),
            new(1, "Gói thầu số 01", 30_000_000_000m),
            new(2, "Đợt 1", 30_000_000_000m),
        },
        Total: 50_000_000_000m,
        TotalInWords: "Năm mươi tỷ đồng");

    private static IXLWorksheet ExcelThongTri(ThongTriForm form, out XLWorkbook workbook)
    {
        var sheet = new ThongTriPrintSheet("Thông tri", PrintPageProfiles.ThongTri, form);
        using var buffer = new MemoryStream();
        new PrintWorkbookWriter().Write(new PrintSheet[] { sheet }, buffer, Footer);
        buffer.Position = 0;
        workbook = new XLWorkbook(buffer);
        return workbook.Worksheets.First();
    }

    private static Table MigraDocThongTri(ThongTriForm form)
    {
        var sheet = new ThongTriPrintSheet("Thông tri", PrintPageProfiles.ThongTri, form);
        return new MigraDocBuilder().Build(new PrintSheet[] { sheet }, Footer)
            .Sections[0]!.Elements.OfType<Table>().First();
    }

    [Fact]
    public void Thong_tri_khop_tung_o_voi_ban_xlsx()
    {
        var form = ThongTriFixture();
        var worksheet = ExcelThongTri(form, out var workbook);
        using (workbook)
            SheetTextMap.AssertSameText("Thông tri", worksheet, MigraDocThongTri(form));
    }

    [Fact]
    public void Thong_tri_co_tam_cot_dung_nhu_Mau()
    {
        MigraDocThongTri(ThongTriFixture()).Columns.Count.ShouldBe(8);
    }

    /// Đầu bảng gộp dọc hai dòng 12–13 như Mẫu.
    [Fact]
    public void Thong_tri_gop_doc_o_Loai_ngan_sach()
    {
        MigraDocThongTri(ThongTriFixture()).Rows[11]!.Cells[0]!.MergeDown.ShouldBe(1);
    }

    /// "Mục lục ngân sách" trải năm cột 2–6 trên đúng dòng 12.
    [Fact]
    public void Thong_tri_gop_ngang_o_Muc_luc_ngan_sach()
    {
        MigraDocThongTri(ThongTriFixture()).Rows[11]!.Cells[1]!.MergeRight.ShouldBe(4);
    }

    [Fact]
    public void Thong_tri_in_doc_tren_A4()
    {
        var sheet = new ThongTriPrintSheet("Thông tri", PrintPageProfiles.ThongTri, ThongTriFixture());
        var result = PdfProbe.Render(new MigraDocBuilder().Build(new PrintSheet[] { sheet }, Footer));

        result.Pages[0].IsLandscape.ShouldBeFalse();
        result.Pages[0].WidthMm.ShouldBe(210.0, 0.5);
    }

    /// Ô thủ tục bỏ trống in "…………" (bốn dấu chấm lửng U+2026, không phải dấu chấm ASCII) cho
    /// người dùng điền tay, đúng như đường .xlsx (FormExcelExporter.Blank). Bản MigraDoc không
    /// được in ô trống.
    [Fact]
    public void Thong_tri_o_thu_tuc_bo_trong_in_dau_cham_lung()
    {
        var form = ThongTriFixture() with { SoThongTri = null, NgayGhiSo = "   " };
        var worksheet = ExcelThongTri(form, out var workbook);
        using (workbook)
            SheetTextMap.AssertSameText("Thông tri", worksheet, MigraDocThongTri(form));
    }

    /// Biểu mẫu không có dòng nội dung nào: builder vẫn phải dựng được (dòng "Cộng" ngay sau
    /// đầu bảng), chứ không ném vì row đếm sai.
    [Fact]
    public void Thong_tri_khong_co_dong_nao_van_dung_duoc()
    {
        var form = ThongTriFixture() with { Lines = Array.Empty<ThongTriLine>(), Total = 0m };
        var worksheet = ExcelThongTri(form, out var workbook);
        using (workbook)
            SheetTextMap.AssertSameText("Thông tri", worksheet, MigraDocThongTri(form));
    }

    /// Phép so phải PHÂN BIỆT được trên ô gộp: xoá chữ của ô gộp "Loại ngân sách" (12,1) ở bản
    /// MigraDoc thì phải đỏ và nêu đúng ô đó. Thông tri là biểu mẫu đầu tiên có ô gộp.
    [Fact]
    public void So_o_phat_hien_thieu_chu_o_o_gop()
    {
        var form = ThongTriFixture();
        var worksheet = ExcelThongTri(form, out var workbook);
        using (workbook)
        {
            var table = MigraDocThongTri(form);
            table.Rows[11]!.Cells[0]!.Elements.Clear();

            var failure = Should.Throw<Xunit.Sdk.XunitException>(
                () => SheetTextMap.AssertSameText("Thông tri", worksheet, table));
            failure.Message.ShouldContain("(12,1)");
            failure.Message.ShouldContain("Loại ngân sách");
        }
    }

    // ===== Cấu trúc bố cục =====
    // So từng ô theo chữ (SheetTextMap) KHÔNG thấy bố cục: ô gộp, đậm, căn giữa, viền đều vô hình
    // với nó. Nhóm test dưới đây là chốt chặn duy nhất cho gộp ô và định dạng; KS chi và GĐNTT
    // được kỳ vọng có nhóm tương tự.

    /// Dòng "Bằng chữ" phụ thuộc số dòng nội dung: 15 (dòng đầu) + Lines.Count là dòng "Cộng",
    /// liền sau là "Bằng chữ". Tính từ fixture, không viết cứng — chính sự phụ thuộc này cũng
    /// cần được ghim.
    private static int BangChuRow(ThongTriForm form) => 15 + form.Lines.Count + 1;

    public static IEnumerable<object[]> MergeAnchors()
    {
        var bangChu = BangChuRow(ThongTriFixture());
        // (dòng, cột, MergeRight, MergeDown)
        yield return new object[] { 4, 1, 7, 0 };
        yield return new object[] { 5, 1, 7, 0 };
        yield return new object[] { 6, 1, 7, 0 };
        yield return new object[] { 12, 1, 0, 1 };
        yield return new object[] { 12, 2, 4, 0 };
        yield return new object[] { 12, 7, 0, 1 };
        yield return new object[] { 12, 8, 0, 1 };
        yield return new object[] { bangChu, 1, 7, 0 };
    }

    [Theory]
    [MemberData(nameof(MergeAnchors))]
    public void Thong_tri_gop_o_dung_neo(int row, int column, int mergeRight, int mergeDown)
    {
        var cell = MigraDocThongTri(ThongTriFixture()).Rows[row - 1]!.Cells[column - 1]!;
        cell.MergeRight.ShouldBe(mergeRight);
        cell.MergeDown.ShouldBe(mergeDown);
    }

    [Fact]
    public void Thong_tri_dau_bang_dam_va_can_giua()
    {
        var table = MigraDocThongTri(ThongTriFixture());
        foreach (var (row, column) in new[] { (12, 1), (13, 3) })
        {
            var cell = table.Rows[row - 1]!.Cells[column - 1]!;
            cell.Format.Font.Bold.ShouldBeTrue($"({row},{column}) phải đậm");
            cell.Elements.OfType<Paragraph>().First().Format.Alignment
                .ShouldBe(ParagraphAlignment.Center, $"({row},{column}) phải căn giữa");
        }
    }

    /// Nửa sau là nửa phân biệt: viền chỉ ở khối 12..Cộng, dòng 1 không có viền.
    [Fact]
    public void Thong_tri_vien_chi_o_khoi_bang()
    {
        var form = ThongTriFixture();
        var table = MigraDocThongTri(form);
        var congRow = BangChuRow(form) - 1;

        table.Rows[congRow - 1]!.Cells[7]!.Borders.Width.Point.ShouldBeGreaterThan(0);
        table.Rows[13]!.Cells[3]!.Borders.Width.Point.ShouldBeGreaterThan(0);
        table.Rows[0]!.Cells[0]!.Borders.Width.Point.ShouldBe(0);
    }

    // ===== KS chi =====

    private static KsChiForm KsChiFixture() => new(
        NgayNhanGiayDeNghi: "07/10/2026",
        MucTieuMucLines: new[] { "Mục 9050, tiểu mục 9062", "", "", "" },
        Accepted: new KsChiAmounts(50_000_000_000m, 30_000_000_000m, 20_000_000_000m),
        Recovered: new KsChiAmounts(10_000_000_000m, 6_000_000_000m, 4_000_000_000m),
        ThuHoiCacNamTruoc: 4_000_000_000m,
        ThuHoiNamNay: 6_000_000_000m,
        ThueGTGT: 5_000_000_000m,
        PaidToBeneficiary: new KsChiAmounts(40_000_000_000m, 24_000_000_000m, 16_000_000_000m),
        PaidInWords: "Bốn mươi tỷ đồng",
        SoTuChoi: 0m,
        LyDoTuChoi: null,
        GhiChu: "không");

    private static IXLWorksheet ExcelKsChi(KsChiForm form, out XLWorkbook workbook)
    {
        var sheet = new KsChiPrintSheet("KS chi", PrintPageProfiles.KsChi, form);
        using var buffer = new MemoryStream();
        new PrintWorkbookWriter().Write(new PrintSheet[] { sheet }, buffer, Footer);
        buffer.Position = 0;
        workbook = new XLWorkbook(buffer);
        return workbook.Worksheets.First();
    }

    private static Table MigraDocKsChi(KsChiForm form)
    {
        var sheet = new KsChiPrintSheet("KS chi", PrintPageProfiles.KsChi, form);
        return new MigraDocBuilder().Build(new PrintSheet[] { sheet }, Footer)
            .Sections[0]!.Elements.OfType<Table>().First();
    }

    [Fact]
    public void Ks_chi_khop_tung_o_voi_ban_xlsx()
    {
        var form = KsChiFixture();
        var worksheet = ExcelKsChi(form, out var workbook);
        using (workbook)
            SheetTextMap.AssertSameText("KS chi", worksheet, MigraDocKsChi(form));
    }

    [Fact]
    public void Ks_chi_co_nam_cot()
    {
        MigraDocKsChi(KsChiFixture()).Columns.Count.ShouldBe(5);
    }

    /// Bốn dòng "+ Mục …, tiểu mục …": dòng nào bỏ trống thì in mẫu chấm, không để trống.
    [Fact]
    public void Ks_chi_bon_dong_muc_tieu_muc_bo_trong_thi_in_mau_cham()
    {
        var form = KsChiFixture() with { MucTieuMucLines = new[] { "", "", "", "" } };
        var worksheet = ExcelKsChi(form, out var workbook);
        using (workbook)
            SheetTextMap.AssertSameText("KS chi", worksheet, MigraDocKsChi(form));
    }

    [Fact]
    public void Ks_chi_in_doc_tren_A4()
    {
        var sheet = new KsChiPrintSheet("KS chi", PrintPageProfiles.KsChi, KsChiFixture());
        PdfProbe.Render(new MigraDocBuilder().Build(new PrintSheet[] { sheet }, Footer))
            .Pages[0].IsLandscape.ShouldBeFalse();
    }

    // ----- KS chi: cấu trúc bố cục -----
    // Như nhóm Thông tri: so chữ từng ô không thấy gộp ô, đậm, căn giữa, viền; nhóm này ghim chúng.

    public static IEnumerable<object[]> KsChiMergeAnchors()
    {
        // (dòng, cột, MergeRight, MergeDown)
        yield return new object[] { 1, 1, 4, 0 };
        yield return new object[] { 6, 1, 1, 0 };
        yield return new object[] { 7, 1, 1, 0 };
        yield return new object[] { 13, 1, 1, 0 };
        yield return new object[] { 17, 1, 1, 0 };
        yield return new object[] { 18, 1, 4, 0 };
        yield return new object[] { 20, 1, 4, 0 };
        yield return new object[] { 22, 2, 3, 0 };
    }

    [Theory]
    [MemberData(nameof(KsChiMergeAnchors))]
    public void Ks_chi_gop_o_dung_neo(int row, int column, int mergeRight, int mergeDown)
    {
        var cell = MigraDocKsChi(KsChiFixture()).Rows[row - 1]!.Cells[column - 1]!;
        cell.MergeRight.ShouldBe(mergeRight);
        cell.MergeDown.ShouldBe(mergeDown);
    }

    [Fact]
    public void Ks_chi_dau_bang_dam_va_can_giua()
    {
        var table = MigraDocKsChi(KsChiFixture());
        foreach (var (row, column) in new[] { (6, 1), (6, 5) })
        {
            var cell = table.Rows[row - 1]!.Cells[column - 1]!;
            cell.Format.Font.Bold.ShouldBeTrue($"({row},{column}) phải đậm");
            cell.Elements.OfType<Paragraph>().First().Format.Alignment
                .ShouldBe(ParagraphAlignment.Center, $"({row},{column}) phải căn giữa");
        }
    }

    /// Nửa sau là nửa phân biệt: viền chỉ ở khối (6,1)-(17,5); dòng 1 và dòng 20 không có viền.
    [Fact]
    public void Ks_chi_vien_chi_o_khoi_bang()
    {
        var table = MigraDocKsChi(KsChiFixture());

        table.Rows[5]!.Cells[0]!.Borders.Width.Point.ShouldBeGreaterThan(0);
        table.Rows[16]!.Cells[4]!.Borders.Width.Point.ShouldBeGreaterThan(0);
        table.Rows[0]!.Cells[0]!.Borders.Width.Point.ShouldBe(0);
        table.Rows[19]!.Cells[0]!.Borders.Width.Point.ShouldBe(0);
    }

    // ===== GĐNTT =====

    private static GdnttForm GdnttFixture() => new(
        DonViCapTren: "SỞ XÂY DỰNG",
        DonViCoSo: "Ban QLDA số 1",
        So: "17",
        NgayThangNam: "07 tháng 10 năm 2026",
        KinhGui: "Kho bạc Nhà nước tỉnh",
        TenDuAnCongTrinh: "Đường ven biển",
        ChuDauTu: "Ban QLDA số 1",
        CanCuQuyetDinh: "123/QĐ-UBND",
        CanCuHopDong: "01/2026/HĐ-XL",
        CanCuGiayDeNghi: "05/GĐN",
        ThuocKeHoach: "năm 2026",
        PackageName: "Gói thầu số 01",
        ContractorName: "Công ty CP Xây dựng Thành Đạt",
        BankAccount: "0123456789",
        TaiNganHang: "Vietcombank chi nhánh tỉnh",
        UnrecoveredAdvanceBefore: 10_000_000_000m,
        ContractTotal: 50_000_000_000m,
        CumulativeBefore: 20_000_000_000m,
        RequestedThisPeriod: 15_000_000_000m,
        RequestedInWords: "Mười lăm tỷ đồng",
        RecoveredThisPeriod: 5_000_000_000m,
        ChuyenTienBaoHanh: 1_000_000_000m,
        ChuyenTienTamGiu: 500_000_000m,
        PaidToContractor: 8_500_000_000m,
        NguonVon: "NSNN");

    private static IXLWorksheet ExcelGdntt(GdnttForm form, out XLWorkbook workbook)
    {
        var sheet = new GdnttPrintSheet("GĐNTT", PrintPageProfiles.Gdntt, form);
        using var buffer = new MemoryStream();
        new PrintWorkbookWriter().Write(new PrintSheet[] { sheet }, buffer, Footer);
        buffer.Position = 0;
        workbook = new XLWorkbook(buffer);
        return workbook.Worksheets.First();
    }

    private static Table MigraDocGdntt(GdnttForm form)
    {
        var sheet = new GdnttPrintSheet("GĐNTT", PrintPageProfiles.Gdntt, form);
        return new MigraDocBuilder().Build(new PrintSheet[] { sheet }, Footer)
            .Sections[0]!.Elements.OfType<Table>().First();
    }

    [Fact]
    public void Gdntt_khop_tung_o_voi_ban_xlsx()
    {
        var form = GdnttFixture();
        var worksheet = ExcelGdntt(form, out var workbook);
        using (workbook)
            SheetTextMap.AssertSameText("GĐNTT", worksheet, MigraDocGdntt(form));
    }

    [Fact]
    public void Gdntt_co_chin_cot()
    {
        MigraDocGdntt(GdnttFixture()).Columns.Count.ShouldBe(9);
    }

    [Fact]
    public void Gdntt_in_ngang_tren_A4()
    {
        var sheet = new GdnttPrintSheet("GĐNTT", PrintPageProfiles.Gdntt, GdnttFixture());
        var result = PdfProbe.Render(new MigraDocBuilder().Build(new PrintSheet[] { sheet }, Footer));

        result.Pages[0].IsLandscape.ShouldBeTrue();
        result.Pages[0].WidthMm.ShouldBe(297.0, 0.5);
    }

    /// Dòng CỘNG (19) phải gộp 4:5 / 6:7 / 8:9 ĐÚNG NHƯ dòng gói thầu (18): trong cùng một
    /// khung kẻ, cột của hai dòng phải thẳng nhau, nếu không bản in lệch nhìn thấy rõ.
    [Theory]
    [InlineData(18)]
    [InlineData(19)]
    public void Gdntt_dong_goi_thau_va_dong_Cong_gop_cot_giong_nhau(int row)
    {
        var table = MigraDocGdntt(GdnttFixture());

        table.Rows[row - 1]!.Cells[0]!.MergeRight.ShouldBe(2);   // NỘI DUNG: 1:3
        table.Rows[row - 1]!.Cells[3]!.MergeRight.ShouldBe(1);   // 4:5
        table.Rows[row - 1]!.Cells[5]!.MergeRight.ShouldBe(1);   // 6:7
        table.Rows[row - 1]!.Cells[7]!.MergeRight.ShouldBe(1);   // 8:9
    }

    /// Sau task này bộ dựng MigraDoc biết cả sáu tài liệu. Cùng luật với
    /// PrintFormSheet_chi_co_ba_bieu_mau_ma_bo_ghi_biet_ve của đường .xlsx: thêm biểu mẫu mới
    /// (KH vốn / Vốn / QĐ…) mà quên hàm vẽ MigraDoc thì test này đỏ TRƯỚC khi người dùng bấm In.
    [Fact]
    public void Bo_dung_MigraDoc_biet_ve_dung_ba_bieu_mau()
    {
        var footer = new PrintFooterInfo("Dự án A", null);
        PrintSheet[] all =
        {
            new ThongTriPrintSheet("Thông tri", PrintPageProfiles.ThongTri, ThongTriFixture()),
            new KsChiPrintSheet("KS chi", PrintPageProfiles.KsChi, KsChiFixture()),
            new GdnttPrintSheet("GĐNTT", PrintPageProfiles.Gdntt, GdnttFixture()),
        };

        Should.NotThrow(() => new MigraDocBuilder().Build(all, footer));
    }

    // ----- GĐNTT: cấu trúc bố cục -----
    // Như hai nhóm trên: so chữ từng ô không thấy gộp ô, đậm, căn giữa, viền; nhóm này ghim chúng.

    public static IEnumerable<object[]> GdnttMergeAnchors()
    {
        // (dòng, cột, MergeRight, MergeDown)
        foreach (var row in new[] { 1, 2, 3 })
            yield return new object[] { row, 4, 5, 0 };
        yield return new object[] { 4, 1, 8, 0 };
        foreach (var row in Enumerable.Range(6, 11))
            yield return new object[] { row, 1, 2, 0 };
        foreach (var row in new[] { 17, 18, 19 })
        {
            yield return new object[] { row, 1, 2, 0 };
            yield return new object[] { row, 4, 1, 0 };
            yield return new object[] { row, 6, 1, 0 };
            yield return new object[] { row, 8, 1, 0 };
        }
        yield return new object[] { 21, 2, 7, 0 };
        yield return new object[] { 29, 2, 7, 0 };
        yield return new object[] { 31, 1, 2, 0 };
        yield return new object[] { 31, 5, 4, 0 };
    }

    [Theory]
    [MemberData(nameof(GdnttMergeAnchors))]
    public void Gdntt_gop_o_dung_neo(int row, int column, int mergeRight, int mergeDown)
    {
        var cell = MigraDocGdntt(GdnttFixture()).Rows[row - 1]!.Cells[column - 1]!;
        cell.MergeRight.ShouldBe(mergeRight);
        cell.MergeDown.ShouldBe(mergeDown);
    }

    [Fact]
    public void Gdntt_dong_tieu_de_bang_dam_va_can_giua()
    {
        var table = MigraDocGdntt(GdnttFixture());
        foreach (var (row, column) in new[] { (17, 1), (17, 4), (17, 6), (17, 8) })
        {
            var cell = table.Rows[row - 1]!.Cells[column - 1]!;
            cell.Format.Font.Bold.ShouldBeTrue($"({row},{column}) phải đậm");
            cell.Elements.OfType<Paragraph>().First().Format.Alignment
                .ShouldBe(ParagraphAlignment.Center, $"({row},{column}) phải căn giữa");
        }
    }

    /// Nửa sau là nửa phân biệt: viền chỉ ở khối (17,1)-(19,9); dòng 1 và dòng 20 không có viền.
    [Fact]
    public void Gdntt_vien_chi_o_khoi_bang()
    {
        var table = MigraDocGdntt(GdnttFixture());

        table.Rows[16]!.Cells[0]!.Borders.Width.Point.ShouldBeGreaterThan(0);
        table.Rows[17]!.Cells[3]!.Borders.Width.Point.ShouldBeGreaterThan(0);
        table.Rows[18]!.Cells[7]!.Borders.Width.Point.ShouldBeGreaterThan(0);
        table.Rows[0]!.Cells[0]!.Borders.Width.Point.ShouldBe(0);
        table.Rows[19]!.Cells[0]!.Borders.Width.Point.ShouldBe(0);
    }
}
