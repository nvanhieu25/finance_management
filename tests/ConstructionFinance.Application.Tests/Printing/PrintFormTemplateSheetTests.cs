using ClosedXML.Excel;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Printing;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// Nhánh FormTemplatePrintSheet của PrintWorkbookWriter: ghi workbook ra bộ nhớ rồi ĐỌC LẠI bằng ClosedXML.
/// Thay PrintFormSheetTests (ba biểu mẫu chép tay) bằng cùng các ý trên bản gốc chung.
public class PrintFormTemplateSheetTests
{
    private static readonly PrintFooterInfo Footer = new("Dự án A", "Vị trí 1");
    private static readonly FormOrgInfo Org = new("Sở Tài chính", "Ban QLDA số 1");

    /// OddPages là thẻ Excel in ra khi không bật "khác nhau chẵn/lẻ" (xem PrintWorkbookWriterTests.PrintedOccurrence).
    private const XLHFOccurrence PrintedOccurrence = XLHFOccurrence.OddPages;

    private static FormDotRow Dot(FormDotKey key, Fund fund, decimal advance, string content) =>
        new(key, "Vị trí 1", "CHI PHÍ XÂY DỰNG", "Gói A", "Cty XL A", "123456789",
            50_000_000_000m, 0m, new DateOnly(2026, 3, 1), content, "CT01", fund, advance, 0m, 0m, advance);

    private static FormBuildContext Context() => new(
        new[]
        {
            Dot(new(0, 0, 0, 0), Fund.NSNN, 10_000_000m, "Tạm ứng lần 1"),
            Dot(new(0, 0, 0, 1), Fund.NgoaiHoi, 30_000_000m, "Ngoại hối"),
        },
        new[] { new FormDotKey(0, 0, 0, 0), new FormDotKey(0, 0, 0, 1) }.ToHashSet(),
        new FormInputSet(), Org, "Dự án A", 2026);

    private static FormTemplatePrintSheet FormSheet(FormDefinition definition, string? name = null)
    {
        var context = definition is GdnttDefinition
            ? Context() with { Selected = new[] { new FormDotKey(0, 0, 0, 1) }.ToHashSet() }
            : Context();
        var sheet = definition.Build(context).Sheet;
        return new FormTemplatePrintSheet(name ?? sheet.SheetName, definition.PrintProfile, sheet);
    }

    private static PrintTableSheet SampleTableSheet() => new(
        "Tổng hợp", PrintPageProfiles.ProjectTongHop,
        new[] { new PrintColumn("Nội dung", 30, PrintAlign.Left), new PrintColumn("Tạm ứng", 15, PrintAlign.Right) },
        new[] { new PrintRow(new[] { PrintCell.OfText("Dự án A"), PrintCell.OfMoney(10_000_000m) }) });

    private static XLWorkbook WriteAndReopen(params PrintSheet[] sheets)
    {
        var stream = new MemoryStream();
        new PrintWorkbookWriter().Write(sheets, stream, Footer);
        stream.Position = 0;
        return new XLWorkbook(stream);
    }

    [Fact]
    public void Ghep_duoc_bieu_mau_va_bang_trong_cung_mot_workbook()
    {
        using var wb = WriteAndReopen(
            SampleTableSheet(),
            FormSheet(FormDefinitions.ThongTri), FormSheet(FormDefinitions.KsChi), FormSheet(FormDefinitions.Gdntt));

        wb.Worksheets.Select(w => w.Name).ShouldBe(new[] { "Tổng hợp", "Thông tri", "KS chi", "GĐNTT" });
        wb.Worksheet("Tổng hợp").Cell(2, 1).GetString().ShouldBe("Dự án A");
        wb.Worksheet("Thông tri").Cell(4, 1).GetString().ShouldBe("THÔNG TRI");
    }

    [Fact]
    public void Ten_sheet_bieu_mau_trung_ten_sheet_bang_thi_van_duoc_danh_so()
    {
        using var wb = WriteAndReopen(
            SampleTableSheet() with { SheetName = "Thông tri" },
            FormSheet(FormDefinitions.ThongTri, "THÔNG TRI"));

        wb.Worksheet(2).Name.ShouldBe("THÔNG TRI (2)");
        wb.Worksheet(2).Cell(4, 1).GetString().ShouldBe("THÔNG TRI");
    }

    [Fact]
    public void Ca_ba_bieu_mau_in_A4_doc_theo_ho_so_cua_chung()
    {
        using var wb = WriteAndReopen(
            FormSheet(FormDefinitions.ThongTri), FormSheet(FormDefinitions.KsChi), FormSheet(FormDefinitions.Gdntt));

        foreach (var ws in wb.Worksheets)
        {
            ws.PageSetup.PageOrientation.ShouldBe(XLPageOrientation.Portrait, ws.Name);
            ws.PageSetup.PaperSize.ShouldBe(XLPaperSize.A4Paper, ws.Name);
            ws.PageSetup.PagesWide.ShouldBe(1, ws.Name);
        }
    }

    [Fact]
    public void Chan_trang_cua_bieu_mau_giong_chan_trang_cua_sheet_bang()
    {
        using var wb = WriteAndReopen(SampleTableSheet(), FormSheet(FormDefinitions.KsChi));

        var table = wb.Worksheet(1).PageSetup.Footer;
        var form = wb.Worksheet(2).PageSetup.Footer;
        form.Left.GetText(PrintedOccurrence).ShouldBe(table.Left.GetText(PrintedOccurrence));
        form.Right.GetText(PrintedOccurrence).ShouldBe(table.Right.GetText(PrintedOccurrence));
        form.Left.GetText(PrintedOccurrence).ShouldContain("Dự án A");
    }

    [Fact]
    public void Bieu_mau_giu_nguyen_duong_ke_cua_chinh_no_va_khong_bi_ke_luoi_cua_bang()
    {
        using var wb = WriteAndReopen(FormSheet(FormDefinitions.ThongTri));
        var ws = wb.Worksheet(1);

        // Dòng tên đơn vị nằm ngoài khung bảng của Thông tri nên không có nét kẻ; lưới của sheet bảng mà tràn sang
        // đây thì Mẫu để trắng chỗ nào cũng bị bao khung.
        var cell = ws.Cell(1, 1).Style.Border;
        new[] { cell.LeftBorder, cell.RightBorder, cell.TopBorder, cell.BottomBorder }
            .ShouldAllBe(side => side == XLBorderStyleValues.None);
        // Khung bảng do chính biểu mẫu kẻ (dòng chữ cái A..G) vẫn nguyên.
        ws.Cell(14, 2).Style.Border.LeftBorder.ShouldBe(XLBorderStyleValues.Thin);
    }

    [Fact]
    public void ApplyPage_ap_hai_lan_len_cung_mot_sheet_cho_ket_qua_nhu_mot_lan()
    {
        var sheet = FormSheet(FormDefinitions.KsChi);
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("KS chi");
        FormExcelExporter.WriteSheet(ws, sheet.Sheet);

        PrintWorkbookWriter.ApplyPage(ws, sheet.Page, Footer, resetFont: false);
        PrintWorkbookWriter.ApplyPage(ws, sheet.Page, Footer, resetFont: false);

        ws.PageSetup.Footer.Left.GetText(PrintedOccurrence).ShouldBe("Dự án A · Vị trí 1");
        ws.Cell(1, 1).Style.Font.FontSize.ShouldBe(13);
    }
}
