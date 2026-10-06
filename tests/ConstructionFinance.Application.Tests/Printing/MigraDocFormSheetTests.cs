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

    /// Ô thủ tục bỏ trống in dấu chấm cho người dùng điền tay, đúng như đường .xlsx
    /// (FormExcelExporter.Blank). Bản MigraDoc không được in ô trống.
    [Fact]
    public void Thong_tri_o_thu_tuc_bo_trong_in_dau_cham()
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
}
