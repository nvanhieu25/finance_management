using ClosedXML.Excel;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Printing;
using ConstructionFinance.Domain.Finance;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

public class MigraDocFormRendererTests
{
    private static readonly FormOrgInfo Org = new("Sở Tài chính", "Ban QLDA số 1");
    private static readonly PrintFooterInfo Footer = new("Dự án A", null);

    /// Hồ sơ như sẽ dùng sau Task 11: ba biểu mẫu dọc; KS chi và GĐNTT ép vừa một trang cả chiều dọc.
    private static readonly PrintPageProfile Thong = new(PrintOrientation.Portrait, 1, 0, 0, 0);
    private static readonly PrintPageProfile OnePage = new(PrintOrientation.Portrait, 1, 1, 0, 0);

    private static FormDotRow Dot(FormDotKey key, Fund fund, decimal advance, string content, string viTri = "Vị trí 1") =>
        new(key, viTri, "CHI PHÍ XÂY DỰNG", "Gói A", "Công ty cổ phần xây dựng và thương mại Hải Phòng",
            "0123456789012", 50_000_000_000m, 2_000_000_000m, new DateOnly(2026, 3, 1), content, "CT01", fund,
            advance, 0m, 0m, advance);

    private static FormBuildContext Context(int dots = 3, FormInputSet? inputs = null) =>
        new(Enumerable.Range(0, dots)
                .Select(i => Dot(new(i % 2, 0, 0, i / 2), i % 2 == 0 ? Fund.NSNN : Fund.NgoaiHoi,
                    10_000_000m * (i + 1), $"Nội dung đợt số {i + 1} khá dài để thử xuống dòng trong ô",
                    $"Vị trí {i % 2 + 1}"))
                .ToList(),
            Enumerable.Range(0, dots).Select(i => new FormDotKey(i % 2, 0, 0, i / 2)).ToHashSet(),
            inputs ?? new FormInputSet(), Org, "Dự án A", 2026);

    private static Document Build(ResolvedSheet sheet, PrintPageProfile profile) =>
        new MigraDocBuilder().Build(
            new PrintSheet[] { new FormTemplatePrintSheet(sheet.SheetName, profile, sheet) }, Footer);

    private static Table TableOf(Document document) =>
        document.Sections[0]!.Elements.OfType<Table>().First();

    // ===== một trang =====

    [Fact]
    public void ThongTri_fits_one_portrait_page_at_the_Mau_widths()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context()).Sheet;

        var probe = PdfProbe.Render(Build(sheet, Thong));

        probe.PageCount.ShouldBe(1);
        probe.Pages[0].IsLandscape.ShouldBeFalse();
        probe.Pages[0].WidthMm.ShouldBe(210.0, 0.5);
    }

    [Fact]
    public void KsChi_fits_one_portrait_page()
    {
        var sheet = FormDefinitions.KsChi.Build(Context()).Sheet;

        PdfProbe.Render(Build(sheet, OnePage)).PageCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Gdntt_fits_one_portrait_page_even_with_long_values(bool longValues)
    {
        var inputs = new FormInputSet();
        if (longValues)
        {
            inputs.Set("TenDuAnCongTrinh", new string('A', 400));
            inputs.Set("TaiNganHang", new string('B', 300));
            inputs.Set("CanCuHopDong", new string('C', 300));
        }
        var context = Context(inputs: inputs) with
        {
            Selected = new[] { new FormDotKey(1, 0, 0, 0) }.ToHashSet()
        };
        var sheet = FormDefinitions.Gdntt.Build(context).Sheet;

        var probe = PdfProbe.Render(Build(sheet, OnePage));

        probe.PageCount.ShouldBe(1);
        probe.Pages[0].IsLandscape.ShouldBeFalse();
    }

    /// Mẫu để trống 8 hàng cuối Thông tri (chỗ ký…): dựng hết các hàng trống đó đẩy tờ sang một trang TRẮNG. Đo được bằng
    /// PDF thật: 3 biểu mẫu ra 4 trang, trang 2 trắng.
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    public void Thong_tri_khong_de_lai_trang_trang_o_cuoi(int dots)
    {
        var sheet = FormDefinitions.ThongTri.Build(Context(dots)).Sheet;

        PdfProbe.Render(Build(sheet, Thong)).PageCount.ShouldBe(1);
    }

    [Fact]
    public void Trailing_empty_rows_are_not_rendered_but_content_rows_are()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context()).Sheet;

        MigraDocFormRenderer.UsedRowCount(sheet).ShouldBe(32);                 // tới dòng họ tên người ký, bỏ hàng 33
        TableOf(Build(sheet, Thong)).Rows.Count.ShouldBe(32);
    }

    /// In thử 2026-10-29: Thông tri sang trang hai thì họ tên người ký nằm lẻ một mình đầu trang sau.
    [Fact]
    public void Khoi_chu_ky_di_lien_voi_hang_cuoi_cua_bang()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context()).Sheet;
        var table = TableOf(Build(sheet, Thong));

        MigraDocFormRenderer.LastBodyRow(sheet).ShouldBe(24);                       // hàng "Cộng"
        table.Rows[23]!.KeepWith.ShouldBe(MigraDocFormRenderer.UsedRowCount(sheet) - 24);
    }

    [Fact]
    public void Thong_tri_nhieu_dot_chay_sang_trang_khong_ne()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context(dots: 60)).Sheet;

        var probe = PdfProbe.Render(Build(sheet, Thong));

        probe.PageCount.ShouldBeGreaterThan(1);
    }

    // ===== tỉ lệ co =====

    [Fact]
    public void Scale_is_one_for_widths_and_only_the_one_page_profile_shrinks_the_tall_gdntt()
    {
        // Bề ngang cả ba vừa A4 dọc nên không co theo bề ngang. Thông tri có khối chữ ký thì chỉ tràn nhẹ một trang,
        // nên co nhẹ cho vừa một tờ (không quá MinOverflowShrink).
        MigraDocFormRenderer.ScaleOf(FormDefinitions.ThongTri.Build(Context()).Sheet, Thong).ShouldBeInRange(0.85, 1.0);
        MigraDocFormRenderer.ScaleOf(FormDefinitions.KsChi.Build(Context()).Sheet, OnePage).ShouldBeInRange(0.9, 1.0);   // chỉ co nhẹ theo biên an toàn chiều dọc
        var gdntt = FormDefinitions.Gdntt.Build(Context()).Sheet;
        MigraDocFormRenderer.ScaleOf(gdntt, Thong).ShouldBe(1.0, 0.0001);
        // Bỏ hàng trống cuối (chỗ ký) thì GĐNTT gần như vừa nguyên cỡ; ép một trang chỉ co khi chữ dài làm tờ cao hơn.
        MigraDocFormRenderer.ScaleOf(gdntt, OnePage).ShouldBeInRange(0.85, 1.0);
    }

    [Fact]
    public void One_page_profile_shrinks_a_sheet_that_is_too_tall()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context(dots: 40)).Sheet;

        MigraDocFormRenderer.ScaleOf(sheet, Thong).ShouldBe(1.0, 0.0001);                // nhiều trang được
        MigraDocFormRenderer.ScaleOf(sheet, OnePage).ShouldBeLessThan(1.0);              // ép một trang thì co
        PdfProbe.Render(Build(sheet, OnePage)).PageCount.ShouldBe(1);
    }

    [Fact]
    public void Footer_font_equals_the_body_font_scale()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context(dots: 40)).Sheet;
        var document = Build(sheet, OnePage);

        var footerPoints = document.Sections[0]!.Footers.Primary.Elements
            .OfType<Paragraph>().First().Format.Font.Size.Point;

        footerPoints.ShouldBe(TableOf(document).Format.Font.Size.Point, 0.01);
    }

    [Theory]
    [InlineData("thongtri")]
    [InlineData("kschi")]
    [InlineData("gdntt")]
    public void Footer_font_equals_the_body_font_for_each_form_and_never_exceeds_13pt(string id)
    {
        var definition = FormDefinitions.All.Single(d => d.Id == id);
        var context = id == "gdntt" ? Context() with { Selected = new[] { new FormDotKey(1, 0, 0, 0) }.ToHashSet() } : Context();
        var sheet = definition.Build(context).Sheet;
        var document = Build(sheet, definition.PrintProfile);

        var footerPoints = document.Sections[0]!.Footers.Primary.Elements.OfType<Paragraph>().First().Format.Font.Size.Point;

        footerPoints.ShouldBe(TableOf(document).Format.Font.Size.Point, 0.01);
        footerPoints.ShouldBeLessThanOrEqualTo(FormExcelExporter.FontSize);
        // Chân trang nằm gọn trong lề dưới cho cả ba tờ người dùng ký (bất biến của Chan_trang_nam_gon_duoi_le_duoi).
        var room = (MigraDocBuilder.VerticalMarginInches - MigraDocBuilder.FooterDistanceInches) * 72.0;
        room.ShouldBeGreaterThanOrEqualTo(footerPoints * 1.15);
    }

    // ===== nội dung =====

    [Theory]
    [InlineData("thongtri")]
    [InlineData("kschi")]
    [InlineData("gdntt")]
    public void Printed_text_matches_the_excel_text_cell_by_cell(string id)
    {
        var definition = FormDefinitions.All.Single(d => d.Id == id);
        var context = Context() with
        {
            Selected = id == "gdntt" ? new[] { new FormDotKey(1, 0, 0, 0) }.ToHashSet() : Context().Selected
        };
        var sheet = definition.Build(context).Sheet;

        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet(sheet.SheetName);
        FormExcelExporter.WriteSheet(ws, sheet);
        var table = TableOf(Build(sheet, id == "thongtri" ? Thong : OnePage));

        SheetTextMap.AssertSameText(sheet.SheetName, ws, table);
    }

    [Fact]
    public void Overflowing_text_cells_merge_to_the_right_but_keep_their_text_at_the_anchor()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context()).Sheet;   // A10 "Tên CTMT…" tràn qua 8 cột
        var table = TableOf(Build(sheet, Thong));

        table.Rows[9]!.Cells[0]!.MergeRight.ShouldBe(7);
    }

    [Fact]
    public void Row_heights_are_at_least_the_template_heights()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context()).Sheet;
        var table = TableOf(Build(sheet, Thong));
        var scale = MigraDocFormRenderer.ScaleOf(sheet, Thong);

        table.Rows[16]!.Height.Point.ShouldBe(47.25 * scale, 0.01);        // hàng 17: đợt, theo tỉ lệ co của cả tờ
        table.Rows[16]!.HeightRule.ShouldBe(RowHeightRule.AtLeast);
    }

    [Theory]
    [InlineData("Ngày        tháng    năm 2026", "Ngày\u00A0\u00A0\u00A0\u00A0\u00A0\u00A0\u00A0\u00A0tháng\u00A0\u00A0\u00A0\u00A0năm 2026")]
    [InlineData(" + Mục ........", "\u00A0+ Mục ........")]
    [InlineData("  Các năm trước", "\u00A0\u00A0Các năm trước")]
    [InlineData("Một dấu cách giữa các từ", "Một dấu cách giữa các từ")]
    [InlineData("", "")]
    public void PreserveSpaces_keeps_the_blanks_the_Mau_leaves_for_handwriting_and_indents(string input, string expected)
    {
        MigraDocFormRenderer.PreserveSpaces(input).ShouldBe(expected);
    }

    [Fact]
    public void Printed_text_keeps_the_gaps_left_for_handwriting()
    {
        var sheet = FormDefinitions.KsChi.Build(Context()).Sheet;
        var table = TableOf(Build(sheet, OnePage));

        var text = SheetTextMap.FromMigraDoc(table);   // đã gộp khoảng trắng để so sánh; đọc thẳng ô gốc ở dưới
        var cell = table.Rows[2]!.Cells[3]!;           // D3: "Ngày        tháng    năm 2026"
        var raw = string.Concat(cell.Elements.OfType<Paragraph>().First().Elements.OfType<Text>().Select(t => t.Content));

        raw.ShouldContain("\u00A0\u00A0\u00A0");
        text.ShouldNotBeEmpty();
    }
}
