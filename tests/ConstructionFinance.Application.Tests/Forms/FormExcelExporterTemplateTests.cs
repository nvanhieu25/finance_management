using ClosedXML.Excel;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Printing;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

public class FormExcelExporterTemplateTests
{
    private static readonly FormOrgInfo Org = new("Sở Tài chính", "Ban QLDA số 1");

    private static FormDotRow Dot(FormDotKey key, Fund fund, decimal advance, string content) =>
        new(key, "Vị trí 1", "CHI PHÍ XÂY DỰNG", "Gói A", "Cty XL A", "123456789",
            50_000_000_000m, 0m, new DateOnly(2026, 3, 1), content, "CT01", fund,
            advance, 0m, 0m, advance);

    private static FormBuildContext Context(FormInputSet? inputs = null) =>
        new(new[]
            {
                Dot(new(0, 0, 0, 0), Fund.NSNN, 10_000_000m, "Tạm ứng lần 1"),
                Dot(new(0, 0, 0, 1), Fund.NgoaiHoi, 30_000_000m, "Ngoại hối"),
            },
            new[] { new FormDotKey(0, 0, 0, 0), new FormDotKey(0, 0, 0, 1) }.ToHashSet(),
            inputs ?? new FormInputSet(), Org, "Dự án A", 2026);

    private static async Task<XLWorkbook> Export(ResolvedSheet sheet)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cf-form-{Guid.NewGuid():N}.xlsx");
        try
        {
            await new FormExcelExporter().ExportAsync(sheet, path);
            using var stream = new MemoryStream(File.ReadAllBytes(path));
            return new XLWorkbook(stream);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Export_writes_widths_heights_merges_and_page_setup_of_the_template()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context()).Sheet;

        using var wb = await Export(sheet);
        var ws = wb.Worksheet("Thông tri");

        ws.Column(7).Width.ShouldBe(35.0, 0.01);
        ws.Row(17).Height.ShouldBe(47.25, 0.01);
        ws.MergedRanges.Select(r => r.RangeAddress.ToStringRelative()).ShouldContain("A4:H4");
        ws.PageSetup.PageOrientation.ShouldBe(XLPageOrientation.Portrait);
        ws.PageSetup.PaperSize.ShouldBe(XLPaperSize.A4Paper);
    }

    [Fact]
    public async Task Export_writes_per_cell_fonts_not_one_size_for_all()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context()).Sheet;

        using var wb = await Export(sheet);
        var ws = wb.Worksheet("Thông tri");

        ws.Cell(4, 1).Style.Font.FontSize.ShouldBe(14);          // THÔNG TRI
        ws.Cell(4, 1).Style.Font.Bold.ShouldBeTrue();
        ws.Cell(12, 1).Style.Font.FontSize.ShouldBe(10);         // Loại ngân sách
        ws.Cell(1, 7).Style.Font.FontSize.ShouldBe(11);          // Mẫu số CQ06-HD
        ws.Cell(4, 1).Style.Font.FontName.ShouldBe("Times New Roman");
    }

    [Fact]
    public async Task Export_writes_computed_money_as_numbers_with_the_template_format()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context()).Sheet;

        using var wb = await Export(sheet);
        var ws = wb.Worksheet("Thông tri");

        ws.Cell(24, 8).GetDouble().ShouldBe(10_000_000d);
        ws.Cell(24, 8).DataType.ShouldBe(XLDataType.Number);
        ws.Cell(24, 8).Style.NumberFormat.Format.ShouldBe("#,##0_);(#,##0)");
        ws.Cell(15, 8).GetDouble().ShouldBe(10_000_000d);
    }

    [Fact]
    public async Task Export_writes_typed_text_and_the_dotted_placeholder_of_the_Mau_for_empty_inputs()
    {
        var inputs = new FormInputSet();
        inputs.Set("SoThongTri", "07/TT");
        var sheet = FormDefinitions.ThongTri.Build(Context(inputs)).Sheet;

        using var wb = await Export(sheet);
        var ws = wb.Worksheet("Thông tri");

        ws.Cell(3, 1).GetString().ShouldBe("Số      07/TT");
        ws.Cell(10, 1).GetString().ShouldStartWith("Tên CTMT, DA.....");   // còn trống → chữ chấm chấm của Mẫu
    }

    [Fact]
    public async Task Export_leaves_a_blank_number_input_empty()
    {
        var sheet = FormDefinitions.KsChi.Build(Context()).Sheet;

        using var wb = await Export(sheet);
        var ws = wb.Worksheet("KS chi");

        ws.Cell(14, 3).IsEmpty().ShouldBeTrue();
    }

    [Fact]
    public async Task Export_draws_thin_and_hair_borders_on_the_right_edges()
    {
        var sheet = FormDefinitions.KsChi.Build(Context()).Sheet;

        using var wb = await Export(sheet);
        var ws = wb.Worksheet("KS chi");

        ws.Cell(6, 3).Style.Border.LeftBorder.ShouldBe(XLBorderStyleValues.Thin);
        ws.Cell(8, 3).Style.Border.TopBorder.ShouldBe(XLBorderStyleValues.Hair);
        ws.Cell(8, 3).Style.Border.LeftBorder.ShouldBe(XLBorderStyleValues.Thin);
    }

    [Fact]
    public async Task Text_starting_with_equals_stays_text()
    {
        var inputs = new FormInputSet();
        inputs.Set("SoThongTri", "=1+1");
        var sheet = FormDefinitions.ThongTri.Build(Context(inputs)).Sheet;

        using var wb = await Export(sheet);
        var cell = wb.Worksheet("Thông tri").Cell(3, 1);

        cell.HasFormula.ShouldBeFalse();
        cell.GetString().ShouldBe("Số      =1+1");   // chữ đứng sau "Số      " nên vốn không bắt đầu bằng "="
        // Ô chỉ có chữ người dùng gõ (không nhãn đứng trước): phải vẫn là chữ.
        var org = new FormOrgInfo("=SUM(A1)", "Ban");
        var withOrg = FormDefinitions.ThongTri.Build(Context() with { Org = org }).Sheet;
        using var wb2 = await Export(withOrg);
        var first = wb2.Worksheet("Thông tri").Cell(1, 1);
        first.HasFormula.ShouldBeFalse();
        first.GetString().ShouldBe("=SUM(A1)");
    }

    [Fact]
    public async Task Gdntt_export_has_the_two_extra_merges_and_money_suffix()
    {
        var context = Context() with { Selected = new[] { new FormDotKey(0, 0, 0, 1) }.ToHashSet() };
        var sheet = FormDefinitions.Gdntt.Build(context).Sheet;

        using var wb = await Export(sheet);
        var ws = wb.Worksheet("GĐNTT");

        var merges = ws.MergedRanges.Select(r => r.RangeAddress.ToStringRelative()).ToList();
        merges.ShouldContain("H13:I13");
        merges.ShouldContain("H25:I25");
        ws.Cell(26, 9).Style.NumberFormat.Format.ShouldBe("#,##0\"đ\"");
        ws.Column(8).Width.ShouldBe(0.1429, 0.01);
    }

    [Fact]
    public void Print_workbook_keeps_each_cell_font_size_for_a_template_sheet()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context()).Sheet;
        var printSheet = new FormTemplatePrintSheet("Thông tri", PrintPageProfiles.ThongTri, sheet);

        using var stream = new MemoryStream();
        new PrintWorkbookWriter().Write(new PrintSheet[] { printSheet }, stream, new PrintFooterInfo("Dự án A", null));
        stream.Position = 0;
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet("Thông tri");

        ws.Cell(4, 1).Style.Font.FontSize.ShouldBe(14);
        ws.Cell(12, 1).Style.Font.FontSize.ShouldBe(10);
        ws.PageSetup.PagesWide.ShouldBe(1);      // hồ sơ căn trang vẫn áp
        ws.PageSetup.Footer.Left.GetText(XLHFOccurrence.OddPages).ShouldContain("Dự án A");
    }
}
