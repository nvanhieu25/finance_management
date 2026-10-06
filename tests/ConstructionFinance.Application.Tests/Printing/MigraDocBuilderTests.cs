using ConstructionFinance.Application.Printing;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// Bộ dựng MigraDoc cho sheet dạng bảng. Kiểm trên CẤU TRÚC Document (rẻ, chính xác) và
/// thêm vài test render ra PDF cho những gì chỉ lộ ra khi dàn trang thật (số trang, hướng giấy).
public class MigraDocBuilderTests
{
    private static readonly PrintFooterInfo Footer = new("Dự án A", "Vị trí 1");

    private static PrintTableSheet Sheet(
        string name = "Sổ Vị trí",
        PrintPageProfile? page = null,
        IReadOnlyList<PrintColumn>? columns = null,
        IReadOnlyList<PrintRow>? rows = null) =>
        new(name,
            page ?? PrintPageProfiles.ViTriLedger,
            columns ?? new PrintColumn[]
            {
                new("Ngày", 11, PrintAlign.Center),
                new("Nội dung", 38, PrintAlign.Left),
                new("Giá trị HĐ", 16, PrintAlign.Right),
            },
            rows ?? new[]
            {
                new PrintRow(new[]
                {
                    PrintCell.OfDate(new DateOnly(2026, 10, 7)),
                    PrintCell.OfText("Gói thầu số 01"),
                    PrintCell.OfMoney(50_000_000_000m),
                }),
            });

    private static Table FirstTable(Document document) =>
        document.Sections[0]!.Elements.OfType<Table>().First();

    // ===== Khổ giấy và hướng giấy =====

    [Fact]
    public void Moi_sheet_mot_Section_rieng()
    {
        var document = new MigraDocBuilder().Build(
            new PrintSheet[] { Sheet("Sổ Vị trí"), Sheet("Quyết toán năm", PrintPageProfiles.YearSettlement) },
            Footer);

        document.Sections.Count.ShouldBe(2);
    }

    [Fact]
    public void Huong_giay_va_le_lay_theo_ho_so_cua_sheet()
    {
        var document = new MigraDocBuilder().Build(new PrintSheet[] { Sheet() }, Footer);

        var setup = document.Sections[0]!.PageSetup;
        setup.PageFormat.ShouldBe(PageFormat.A4);
        setup.Orientation.ShouldBe(Orientation.Landscape);
        setup.LeftMargin.Inch.ShouldBe(ExcelFitScale.SideMarginInches, 0.001);
        setup.RightMargin.Inch.ShouldBe(ExcelFitScale.SideMarginInches, 0.001);
    }

    // ===== Cột, chữ, kiểu =====

    [Fact]
    public void Be_rong_cot_co_theo_dung_ti_le_cua_ho_so()
    {
        var document = new MigraDocBuilder().Build(new PrintSheet[] { Sheet() }, Footer);
        var table = FirstTable(document);

        var scale = ExcelFitScale.ScaleFor(new double[] { 11, 38, 16 }, PrintOrientation.Landscape);
        table.Columns.Count.ShouldBe(3);
        table.Columns[0]!.Width.Millimeter.ShouldBe((11 * 7 + 5) * 25.4 / 96 * scale, 0.01);
    }

    [Fact]
    public void Co_chu_co_theo_cung_ti_le_ay()
    {
        var document = new MigraDocBuilder().Build(new PrintSheet[] { Sheet() }, Footer);

        var scale = ExcelFitScale.ScaleFor(new double[] { 11, 38, 16 }, PrintOrientation.Landscape);
        document.Sections[0]!.PageSetup.Orientation.ShouldBe(Orientation.Landscape);
        FirstTable(document).Format.Font.Size.Point.ShouldBe(13 * scale, 0.01);
    }

    [Fact]
    public void Dong_dau_la_tieu_de_cot_in_dam_va_duoc_lap_lai_moi_trang()
    {
        var document = new MigraDocBuilder().Build(new PrintSheet[] { Sheet() }, Footer);
        var table = FirstTable(document);
        var header = table.Rows[0]!;

        CellText(table, 0, 0).ShouldBe("Ngày");
        header.Format.Font.Bold.ShouldBeTrue();
        // HeadingFormat là cách MigraDoc lặp dòng này ở đầu MỖI trang — đúng việc
        // PageSetup.SetRowsToRepeatAtTop làm cho đường .xlsx. Ghim ở mức mô hình, vì PDFsharp
        // không trích được chữ từ PDF để đếm xem dòng đó có thật xuất hiện lại ở trang 2.
        header.HeadingFormat.ShouldBeTrue();
    }

    [Fact]
    public void Tien_in_theo_quy_uoc_Viet_khong_bao_gio_thanh_dau_thang()
    {
        var document = new MigraDocBuilder().Build(new PrintSheet[] { Sheet() }, Footer);

        CellText(FirstTable(document), 1, 2).ShouldBe("50.000.000.000");
    }

    [Fact]
    public void Ngay_in_theo_dinh_dang_ngay_thang_nam()
    {
        var document = new MigraDocBuilder().Build(new PrintSheet[] { Sheet() }, Footer);

        CellText(FirstTable(document), 1, 0).ShouldBe("07/10/2026");
    }

    [Fact]
    public void O_tien_bang_khong_de_trong()
    {
        var rows = new[] { new PrintRow(new[] { PrintCell.Empty, PrintCell.OfText("x"), PrintCell.OfMoney(0m) }) };
        var document = new MigraDocBuilder().Build(new PrintSheet[] { Sheet(rows: rows) }, Footer);

        CellText(FirstTable(document), 1, 2).ShouldBe("");
    }

    [Theory]
    [InlineData(PrintShade.Light, "#D9D9D9")]
    [InlineData(PrintShade.VeryLight, "#F2F2F2")]
    public void Hai_bac_xam_dung_bang_hai_bac_cua_duong_xlsx(PrintShade shade, string hex)
    {
        var rows = new[] { new PrintRow(new[] { PrintCell.OfText("x") }, Shade: shade) };
        var document = new MigraDocBuilder().Build(new PrintSheet[] { Sheet(rows: rows) }, Footer);

        FirstTable(document).Rows[1]!.Shading.Color.ShouldBe(Color.Parse(hex));
    }

    // ===== Review Focus =====

    /// Review Focus 4a: người dùng mở một Vị trí chưa gõ gì rồi bấm In.
    [Fact]
    public void Bang_khong_co_dong_nao_van_dung_duoc()
    {
        var document = new MigraDocBuilder().Build(
            new PrintSheet[] { Sheet(rows: Array.Empty<PrintRow>()) }, Footer);

        FirstTable(document).Rows.Count.ShouldBe(1);         // chỉ còn dòng tiêu đề
        PdfProbe.Render(document).PageCount.ShouldBe(1);
    }

    /// Review Focus 4b: Table không có cột mà AddRow() thì Cells[0] ném
    /// ArgumentOutOfRangeException. Bảng 0 cột phải thành một Section rỗng, không phải một lần sập.
    [Fact]
    public void Bang_khong_co_cot_nao_khong_lam_sap_bo_dung()
    {
        var sheet = Sheet(columns: Array.Empty<PrintColumn>(), rows: Array.Empty<PrintRow>());

        var document = Should.NotThrow(() => new MigraDocBuilder().Build(new PrintSheet[] { sheet }, Footer));

        document.Sections.Count.ShouldBe(1);
        document.Sections[0]!.Elements.OfType<Table>().ShouldBeEmpty();
    }

    /// Review Focus 3: chân trang mang ký tự lạ. Đường .xlsx phải viết "&&" và cắt ở 255 ký tự
    /// vì Excel coi & là mã điều khiển; MigraDoc không có luật đó, nhưng cũng không được ném.
    [Theory]
    [InlineData("Dự án A & B")]
    [InlineData("&P&N&D")]
    [InlineData("\ud83d")]                                  // nửa cặp surrogate, đứt đôi
    public void Chan_trang_mang_ky_tu_la_khong_lam_sap_bo_dung(string projectName)
    {
        var document = Should.NotThrow(() => new MigraDocBuilder()
            .Build(new PrintSheet[] { Sheet() }, new PrintFooterInfo(projectName, null)));

        document.Sections[0]!.Footers.Primary.ShouldNotBeNull();
    }

    [Fact]
    public void Ten_du_an_dai_ba_tram_ky_tu_khong_lam_sap_bo_dung()
    {
        var footer = new PrintFooterInfo(new string('A', 300), new string('B', 300));

        Should.NotThrow(() => new MigraDocBuilder().Build(new PrintSheet[] { Sheet() }, footer));
    }

    /// Review Focus 5: một ô chữ dài hơn cả trang sau khi co. MigraDoc phải xuống dòng và
    /// sang trang, không tràn ra ngoài giấy và không làm bộ phân trang quay vô hạn.
    [Fact]
    public void O_chu_dai_hon_ca_trang_thi_xuong_dong_chu_khong_treo()
    {
        var rows = new[]
        {
            new PrintRow(new[]
            {
                PrintCell.Empty,
                PrintCell.OfText(string.Join(" ", Enumerable.Repeat("khoản mục xây lắp", 60))),
                PrintCell.Empty,
            }),
        };

        var result = PdfProbe.Render(new MigraDocBuilder().Build(new PrintSheet[] { Sheet(rows: rows) }, Footer));

        result.PageCount.ShouldBeGreaterThanOrEqualTo(1);
        result.PageCount.ShouldBeLessThan(10);              // xuống dòng, không nổ số trang
        result.Pages[0].IsLandscape.ShouldBeTrue();
    }

    /// Dòng nhiều ô hơn số cột là lỗi của bên dựng PrintRow, và đường .xlsx đã ném
    /// ArgumentException có kèm tên sheet. Bản MigraDoc không được im lặng cắt bớt.
    [Fact]
    public void Dong_nhieu_o_hon_so_cot_thi_neu_ten_sheet_trong_loi()
    {
        var rows = new[] { new PrintRow(new[] { PrintCell.OfText("a"), PrintCell.OfText("b"), PrintCell.OfText("c"), PrintCell.OfText("d") }) };

        var ex = Should.Throw<ArgumentException>(() =>
            new MigraDocBuilder().Build(new PrintSheet[] { Sheet("Sổ Vị trí", rows: rows) }, Footer));

        ex.Message.ShouldContain("Sổ Vị trí");
    }

    /// Excel thụt CẢ nội dung ô (Alignment.Indent), kể cả dòng đã xuống dòng. MigraDoc có hai
    /// thuộc tính khác nhau: LeftIndent thụt mọi dòng, FirstLineIndent chỉ thụt dòng đầu. Dùng sai
    /// thì một ô Nội dung dài xuống dòng sẽ có dòng thứ hai tụt về sát mép ô — lệch so với bản
    /// .xlsx, và phép so từng ô của Task 5 KHÔNG bắt được vì nó so chữ, không so hình học.
    [Fact]
    public void Thut_bac_thut_moi_dong_chu_khong_chi_dong_dau()
    {
        var rows = new[]
        {
            new PrintRow(new[]
            {
                PrintCell.Empty,
                PrintCell.OfText("Gói thầu số 01 — xây lắp phần ngầm, đoạn Km0+000 đến Km1+200"),
                PrintCell.OfMoney(1_000_000m),
            }, IndentLevel: 2),
        };

        var document = new MigraDocBuilder().Build(new PrintSheet[] { Sheet(rows: rows) }, Footer);
        var format = FirstTable(document).Rows[1]!.Cells[1]!.Elements.OfType<Paragraph>().First().Format;

        format.LeftIndent.Point.ShouldBeGreaterThan(0);
        format.FirstLineIndent.Point.ShouldBe(0);
    }

    // ===== Dàn trang thật =====

    [Fact]
    public void In_toan_bo_gom_moi_sheet_vao_mot_tai_lieu_moi_sheet_giu_huong_giay_rieng()
    {
        var document = new MigraDocBuilder().Build(
            new PrintSheet[]
            {
                Sheet("Sổ Vị trí", PrintPageProfiles.ViTriLedger),
                Sheet("Quyết toán năm", PrintPageProfiles.YearSettlement),
            },
            Footer);

        var result = PdfProbe.Render(document);

        result.Pages[0].IsLandscape.ShouldBeTrue();
        result.Pages[^1].IsLandscape.ShouldBeFalse();
    }

    [Fact]
    public void Bang_dai_sang_nhieu_trang()
    {
        var rows = Enumerable.Range(0, 200)
            .Select(i => new PrintRow(new[]
            {
                PrintCell.OfDate(new DateOnly(2026, 10, 7)),
                PrintCell.OfText($"Gói thầu số {i:00}"),
                PrintCell.OfMoney(1_000_000m),
            }))
            .ToArray();

        PdfProbe.Render(new MigraDocBuilder().Build(new PrintSheet[] { Sheet(rows: rows) }, Footer))
            .PageCount.ShouldBeGreaterThan(1);
    }

    private static string CellText(Table table, int row, int column)
    {
        var cell = table.Rows[row]!.Cells[column]!;
        var paragraph = cell.Elements.OfType<Paragraph>().FirstOrDefault();
        if (paragraph is null) return "";
        return string.Concat(paragraph.Elements.OfType<Text>().Select(text => text.Content));
    }
}
