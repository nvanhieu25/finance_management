using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Printing;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// Lưới ô thưa dùng để chép bố cục ba biểu mẫu từ ClosedXML sang MigraDoc.
public class FormGridTests
{
    private static Table Render(FormGrid grid, PrintOrientation orientation = PrintOrientation.Portrait)
    {
        var document = new Document();
        var section = document.AddSection();
        return grid.Render(section, orientation);
    }

    private static string CellText(Table table, int row, int column)
    {
        var cell = table.Rows[row - 1]!.Cells[column - 1]!;
        return string.Concat(cell.Elements.OfType<Paragraph>()
            .Select(p => string.Concat(p.Elements.OfType<Text>().Select(t => t.Content))));
    }

    [Fact]
    public void Toa_do_mot_based_nhu_ClosedXML()
    {
        var grid = new FormGrid(new double[] { 9, 9 }).Text(1, 1, "góc trên trái");

        CellText(Render(grid), 1, 1).ShouldBe("góc trên trái");
    }

    [Fact]
    public void Ghi_o_o_dong_xa_tu_dong_them_cac_dong_o_giua()
    {
        var grid = new FormGrid(new double[] { 9, 9 }).Text(5, 2, "x");
        var table = Render(grid);

        table.Rows.Count.ShouldBe(5);
        CellText(table, 5, 2).ShouldBe("x");
        CellText(table, 3, 1).ShouldBe("");
    }

    [Fact]
    public void Tien_dinh_dang_theo_quy_uoc_Viet()
    {
        var grid = new FormGrid(new double[] { 18 }).Money(1, 1, 1_500_000m);

        CellText(Render(grid), 1, 1).ShouldBe("1.500.000");
    }

    [Fact]
    public void Gop_o_tuong_minh_dung_bang_lenh_Merge_cua_Excel()
    {
        // ws.Range(4, 1, 4, 3).Merge() → một ô trải ba cột
        var grid = new FormGrid(new double[] { 9, 9, 9 }).Text(4, 1, "THÔNG TRI").Merge(4, 1, 4, 3);

        Render(grid).Rows[3]!.Cells[0]!.MergeRight.ShouldBe(2);
    }

    [Fact]
    public void Gop_o_doc_dung_bang_Merge_nhieu_dong()
    {
        // ws.Range(12, 1, 13, 1).Merge() → một ô cao hai dòng
        var grid = new FormGrid(new double[] { 9 }).Text(12, 1, "Loại ngân sách").Merge(12, 1, 13, 1);

        Render(grid).Rows[11]!.Cells[0]!.MergeDown.ShouldBe(1);
    }

    // ===== Luật bù tràn chữ =====

    /// Excel cho chữ tràn sang ô trống bên phải; MigraDoc xuống dòng trong lòng ô. Không bù
    /// thì cả ba biểu mẫu vỡ bố cục. 44 ký tự trong cột rộng 34 phải tự gộp sang phải.
    [Fact]
    public void Chu_dai_hon_cot_tu_gop_sang_cac_o_trong_ben_phai()
    {
        var grid = new FormGrid(new double[] { 34, 14, 18 })
            .Text(3, 1, "Ngày nhận giấy đề nghị thanh toán vốn đầu tư:");

        Render(grid).Rows[2]!.Cells[0]!.MergeRight.ShouldBe(2);
    }

    /// …nhưng dừng lại trước ô CÓ CHỮ: Excel cũng cắt chữ ở đúng chỗ đó.
    [Fact]
    public void Tu_gop_dung_truoc_o_da_co_chu()
    {
        var grid = new FormGrid(new double[] { 34, 14, 18 })
            .Text(3, 1, "Ngày nhận giấy đề nghị thanh toán vốn đầu tư:")
            .Text(3, 3, "Ngày ………");

        Render(grid).Rows[2]!.Cells[0]!.MergeRight.ShouldBe(1);
    }

    [Fact]
    public void Chu_vua_cot_thi_khong_tu_gop()
    {
        var grid = new FormGrid(new double[] { 34, 14 }).Text(1, 1, new string('x', 34));

        Render(grid).Rows[0]!.Cells[0]!.MergeRight.ShouldBe(0);
    }

    [Fact]
    public void Gop_tuong_minh_thang_luat_tu_gop()
    {
        var grid = new FormGrid(new double[] { 9, 9, 9 })
            .Text(1, 1, "một dòng chữ rất dài vượt xa bề rộng chín đơn vị")
            .Merge(1, 1, 1, 2);

        Render(grid).Rows[0]!.Cells[0]!.MergeRight.ShouldBe(1);   // 1, không phải 2
    }

    // ===== Kiểu =====

    [Fact]
    public void Bold_dat_cho_ca_dai_o()
    {
        var grid = new FormGrid(new double[] { 9, 9 }).Text(1, 1, "a").Text(1, 2, "b").Bold(1, 1, 1, 2);
        var table = Render(grid);

        table.Rows[0]!.Cells[0]!.Format.Font.Bold.ShouldBeTrue();
        table.Rows[0]!.Cells[1]!.Format.Font.Bold.ShouldBeTrue();
    }

    [Fact]
    public void Border_ke_khung_ca_khoi()
    {
        var grid = new FormGrid(new double[] { 9, 9 }).Text(1, 1, "a").Border(1, 1, 2, 2);
        var table = Render(grid);

        table.Rows[0]!.Cells[0]!.Borders.Width.Point.ShouldBeGreaterThan(0);
        table.Rows[1]!.Cells[1]!.Borders.Width.Point.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Lenh_ghi_sau_thang_lenh_ghi_truoc_tren_cung_mot_o()
    {
        var grid = new FormGrid(new double[] { 9 }).Text(1, 1, "cũ").Text(1, 1, "mới");

        CellText(Render(grid), 1, 1).ShouldBe("mới");
    }

    [Fact]
    public void Luoi_khong_co_cot_nao_la_loi_cua_ben_goi()
    {
        Should.Throw<ArgumentException>(() => new FormGrid(Array.Empty<double>()));
    }

    [Fact]
    public void Ghi_ngoai_so_cot_la_loi_cua_ben_goi()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new FormGrid(new double[] { 9 }).Text(1, 2, "x"));
    }

    [Fact]
    public void Tien_canh_phai_nhu_Excel_canh_so()
    {
        var grid = new FormGrid(new double[] { 18 }).Money(1, 1, 1_500_000m);

        Render(grid).Rows[0]!.Cells[0]!.Elements.OfType<Paragraph>().Single()
            .Format.Alignment.ShouldBe(ParagraphAlignment.Right);
    }

    [Fact]
    public void Chu_khong_canh_phai()
    {
        var grid = new FormGrid(new double[] { 18 }).Text(1, 1, "1.500.000");

        Render(grid).Rows[0]!.Cells[0]!.Elements.OfType<Paragraph>().Single()
            .Format.Alignment.ShouldNotBe(ParagraphAlignment.Right);
    }

    /// Bộ vẽ .xlsx ghi `?? ""` vào ô; trong Excel ô rỗng không chặn chữ tràn.
    [Fact]
    public void O_ghi_chuoi_rong_khong_chan_chu_tran()
    {
        var grid = new FormGrid(new double[] { 34, 14, 18 })
            .Text(3, 1, "Ngày nhận giấy đề nghị thanh toán vốn đầu tư:")
            .Text(3, 2, "")
            .Text(3, 3, "Ngày ………");

        Render(grid).Rows[2]!.Cells[0]!.MergeRight.ShouldBe(1);
    }

    private static Paragraph FirstParagraph(Table table, int row, int column) =>
        table.Rows[row - 1]!.Cells[column - 1]!.Elements.OfType<Paragraph>().First();

    [Fact]
    public void Chu_dai_dung_bang_cot_thi_khong_tu_gop_va_dai_hon_mot_ky_tu_thi_gop()
    {
        var grid = new FormGrid(new double[] { 10, 10 }).Text(1, 1, new string('x', 10)).Text(2, 1, new string('x', 11));
        var table = Render(grid);

        table.Rows[0]!.Cells[0]!.MergeRight.ShouldBe(0);
        table.Rows[1]!.Cells[0]!.MergeRight.ShouldBe(1);
    }

    [Fact]
    public void Tu_gop_dung_truoc_goc_cua_lenh_gop_tuong_minh_dang_trong()
    {
        var grid = new FormGrid(new double[] { 10, 10, 10 })
            .Text(1, 1, new string('x', 40))
            .Merge(1, 2, 1, 3);

        var table = Render(grid);
        table.Rows[0]!.Cells[0]!.MergeRight.ShouldBe(0);
        table.Rows[0]!.Cells[1]!.MergeRight.ShouldBe(1);
    }

    [Fact]
    public void Tu_gop_dung_truoc_o_bi_MergeDown_cua_dong_tren_phu_len()
    {
        var grid = new FormGrid(new double[] { 10, 10, 10 })
            .Text(1, 2, "tiêu đề").Merge(1, 2, 2, 2)
            .Text(2, 1, new string('x', 40));

        Render(grid).Rows[1]!.Cells[0]!.MergeRight.ShouldBe(0);
    }

    [Fact]
    public void Indent_dich_moi_dong_bang_LeftIndent_khong_phai_FirstLineIndent()
    {
        var grid = new FormGrid(new double[] { 9, 9 }).Text(1, 1, "a").Indent(1, 1, 2);

        var format = FirstParagraph(Render(grid), 1, 1).Format;
        format.LeftIndent.Point.ShouldBeGreaterThan(0);
        format.FirstLineIndent.Point.ShouldBe(0);
    }

    [Fact]
    public void Center_canh_giua_dung_o_duoc_chon()
    {
        var grid = new FormGrid(new double[] { 9, 9 }).Text(1, 1, "a").Text(1, 2, "b").Center(1, 1, 1, 1);
        var table = Render(grid);

        FirstParagraph(table, 1, 1).Format.Alignment.ShouldBe(ParagraphAlignment.Center);
        FirstParagraph(table, 1, 2).Format.Alignment.ShouldNotBe(ParagraphAlignment.Center);
    }

    [Fact]
    public void Co_chu_va_be_rong_cot_co_theo_ti_le_vua_kho_giay()
    {
        var widths = new double[] { 60, 60, 60 };
        var scale = ExcelFitScale.ScaleFor(widths, PrintOrientation.Portrait);
        scale.ShouldBeLessThan(1.0);

        var table = Render(new FormGrid(widths).Text(1, 1, "a"));

        table.Format.Font.Size.Point.ShouldBe(FormExcelExporter.FontSize * scale, 0.001);
        table.Columns[0].Width.Millimeter.ShouldBe(ExcelFitScale.ContentWidthMm(new[] { 60.0 }) * scale, 0.001);
    }

    [Fact]
    public void Tien_roi_chu_thi_het_canh_phai()
    {
        var grid = new FormGrid(new double[] { 18 }).Money(1, 1, 5m).Text(1, 1, "x");

        FirstParagraph(Render(grid), 1, 1).Format.Alignment.ShouldNotBe(ParagraphAlignment.Right);
    }

    [Fact]
    public void Chu_roi_tien_thi_canh_phai()
    {
        var grid = new FormGrid(new double[] { 18 }).Text(1, 1, "x").Money(1, 1, 5m);

        FirstParagraph(Render(grid), 1, 1).Format.Alignment.ShouldBe(ParagraphAlignment.Right);
    }

    [Fact]
    public void Bold_voi_cot_bat_dau_ngoai_luoi_la_loi_cua_ben_goi()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new FormGrid(new double[] { 9 }).Bold(1, 0, 1, 1));
    }
}
