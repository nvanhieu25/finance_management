using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Printing;
using ConstructionFinance.Domain.Finance;
using PdfSharp.Pdf.IO;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

public class PdfExporterTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"cf-pdf-{Guid.NewGuid():N}.pdf");

    // Bộ phân giải phông của PDFsharp là trạng thái TĨNH của cả tiến trình và chỉ gán được một lần: để PdfProbe (dùng chung
    // cho cả bộ test) gán trước, PdfExporter thấy đã có thì không gán nữa. Bộ phân giải của app được thử riêng bên dưới.
    public PdfExporterTests() => PdfProbe.EnsureFonts();

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private static PrintSheet[] Sheets()
    {
        var rows = new[]
        {
            new FormDotRow(new FormDotKey(0, 0, 0, 0), "Vị trí 1", "CHI PHÍ XÂY DỰNG", "Gói A", "Cty XL A", "123456789",
                50_000_000_000m, 0m, new DateOnly(2026, 3, 1), "Tạm ứng lần 1", "CT01", Fund.NSNN,
                10_000_000m, 0m, 0m, 10_000_000m)
        };
        var context = new FormBuildContext(rows, rows.Select(r => r.Key).ToHashSet(), new FormInputSet(),
            new FormOrgInfo("Sở Tài chính", "Ban QLDA số 1"), "Dự án A", 2026);
        var sheet = FormDefinitions.ThongTri.Build(context).Sheet;
        return new PrintSheet[]
        {
            new FormTemplatePrintSheet(sheet.SheetName, FormDefinitions.ThongTri.PrintProfile, sheet),
            new FormTemplatePrintSheet("KS chi", FormDefinitions.KsChi.PrintProfile,
                FormDefinitions.KsChi.Build(context).Sheet),
        };
    }

    [Fact]
    public async Task Writes_a_valid_pdf_with_one_page_per_form_sheet()
    {
        var document = new MigraDocBuilder().Build(Sheets(), new PrintFooterInfo("Dự án A", null));

        await new PdfExporter().WriteAsync(document, _path);

        File.ReadAllBytes(_path).Take(5).ShouldBe("%PDF-"u8.ToArray());
        using var reopened = PdfReader.Open(_path, PdfDocumentOpenMode.Import);
        reopened.PageCount.ShouldBe(2);
        reopened.Pages[0].Width.Millimeter.ShouldBe(210.0, 0.5);   // A4 dọc
    }

    [Fact]
    public async Task A_path_that_cannot_be_written_throws_an_IOException_and_leaves_no_file()
    {
        var document = new MigraDocBuilder().Build(Sheets(), new PrintFooterInfo("Dự án A", null));
        var bad = Path.Combine(Path.GetTempPath(), $"khong-co-{Guid.NewGuid():N}", "x.pdf");

        await Should.ThrowAsync<IOException>(() => new PdfExporter().WriteAsync(document, bad));

        File.Exists(bad).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void SystemFontResolver_finds_a_serif_font_file_for_every_style(bool bold, bool italic)
    {
        var resolver = new SystemFontResolver();

        var info = resolver.ResolveTypeface("Times New Roman", bold, italic);
        var bytes = resolver.GetFont(info!.FaceName);

        bytes.ShouldNotBeNull();
        bytes!.Length.ShouldBeGreaterThan(10_000);                  // một tệp TrueType thật, không phải rỗng
    }
}
