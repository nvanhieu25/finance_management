// Sinh từ reference/Mẫu.xlsx bởi tools/gen_form_templates.py MỘT LẦN. Từ nay sửa tay tệp này;
// TemplateMatchesMauTests so nó với Mẫu.xlsx để không trôi khỏi mẫu.
using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Application.Forms;

/// Bản gốc của sheet "Thông tri" trong Mẫu.xlsx.
public static class ThongTriTemplate
{
    private static CellStyle S(double size, bool bold = false, bool italic = false, bool underline = false,
        CellAlign align = CellAlign.General, CellVAlign v = CellVAlign.Bottom, bool wrap = false,
        string borders = "", string? format = null) =>
        Tpl.Style(size, bold, italic, underline, align, v, wrap, borders, format);

    public static FormTemplate Create() => new(
        SheetName: "Thông tri",
        Orientation: PrintOrientation.Portrait,
        ColumnWidths: new double[] { 6, 6, 6, 6, 6, 6, 35, 16.4286 },
        RowHeights: new Dictionary<int, double>
        {
            [1] = 16.5, [2] = 16.5, [3] = 18.75, [4] = 27, [5] = 22.5, [6] = 24.75,
            [7] = 18.75, [8] = 21.95, [9] = 21.95, [10] = 21.95, [11] = 18.75, [12] = 20.25,
            [13] = 25.5, [24] = 30.75, [25] = 29.25, [26] = 20.25, [27] = 21, [28] = 16.5,
            [29] = 16.5, [30] = 16.5, [31] = 16.5, [32] = 17.25, [33] = 17.25,
        },
        Cells: new[]
        {
            Tpl.Merged(1, 1, 1, 6, S(13, align: CellAlign.Center), Tpl.Setting("don_vi_cap_tren", "Đơn vị cấp trên")),
            Tpl.Merged(1, 7, 1, 2, S(11, bold: true, align: CellAlign.Right), Tpl.Fixed("Mẫu số CQ06-HD")),
            Tpl.Merged(2, 1, 1, 6, S(13, bold: true, align: CellAlign.Center), Tpl.Setting("don_vi_co_so", "Đơn vị cơ sở")),
            Tpl.Merged(2, 7, 1, 2, S(11, italic: true, align: CellAlign.Right), Tpl.Fixed("Số:"), Tpl.Input("SoPhieu", "……..…....……")),
            Tpl.Merged(3, 1, 1, 6, S(14, align: CellAlign.Center), Tpl.Fixed("Số      "), Tpl.Input("SoThongTri", ".............")),
            Tpl.Merged(3, 7, 1, 2, S(11, italic: true, align: CellAlign.Right), Tpl.Fixed("Ghi sổ ngày"), Tpl.Input("NgayGhiSo", "     tháng     năm {năm}")),
            Tpl.Merged(4, 1, 1, 8, S(14, bold: true, align: CellAlign.Center), Tpl.Fixed("THÔNG TRI")),
            Tpl.Merged(5, 1, 1, 8, S(14, bold: true, align: CellAlign.Center, v: CellVAlign.Center, wrap: true), Tpl.Fixed("Thu hồi tạm ứng")),
            Tpl.Merged(6, 1, 1, 8, S(14, bold: true, align: CellAlign.Center, v: CellVAlign.Center), Tpl.Fixed("Kinh phí: NSNN")),
            Tpl.Merged(7, 1, 1, 8, S(14, bold: true, align: CellAlign.Center)),
            Tpl.Merged(8, 1, 1, 2, S(13, bold: true, align: CellAlign.Left, v: CellVAlign.Center), Tpl.Fixed("Đơn vị:                         ")),
            Tpl.Cell(8, 3, S(13, bold: true, v: CellVAlign.Center), Tpl.Input("DonVi", "….......")),
            Tpl.Merged(8, 7, 1, 2, S(13, align: CellAlign.Left), Tpl.Fixed(" TK"), Tpl.Input("TaiKhoan1", "……………….………...…"), Tpl.Fixed("Đơn vị"), Tpl.Input("DonVi1", "……….."), Tpl.Fixed("Số CT"), Tpl.Input("SoChungTu1", "……..")),
            Tpl.Merged(9, 1, 1, 6, S(13, bold: true, align: CellAlign.Left), Tpl.Fixed("Về"), Tpl.Input("VeThangNam", " tháng      năm {năm}")),
            Tpl.Merged(9, 7, 1, 2, S(13, align: CellAlign.Left), Tpl.Fixed(" TK"), Tpl.Input("TaiKhoan2", "………………………....…"), Tpl.Fixed("Đơn vị"), Tpl.Input("DonVi2", "……….."), Tpl.Fixed("Số CT"), Tpl.Input("SoChungTu2", "…….")),
            Tpl.Cell(10, 1, S(13), Tpl.Fixed("Tên CTMT, DA"), Tpl.Input("TenCTMT", "....................................................................."), Tpl.Fixed(" Mã số: "), Tpl.Input("MaSo", "............................")),
            Tpl.Merged(11, 7, 1, 2, S(12, italic: true, align: CellAlign.Right, borders: "b"), Tpl.Fixed("Đơn vị tính: Đồng")),
            Tpl.Merged(12, 1, 2, 1, S(10, bold: true, align: CellAlign.Center, v: CellVAlign.Center, wrap: true, borders: "lrtb"), Tpl.Fixed("Loại ngân sách")),
            Tpl.Merged(12, 2, 1, 5, S(10, bold: true, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrtb"), Tpl.Fixed("Mục lục ngân sách")),
            Tpl.Merged(12, 7, 2, 1, S(11, bold: true, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrtb"), Tpl.Fixed("NỘI DUNG")),
            Tpl.Merged(12, 8, 2, 1, S(11, bold: true, align: CellAlign.Center, v: CellVAlign.Center, wrap: true, borders: "lrtb"), Tpl.Fixed("SỐ TIỀN")),
            Tpl.Cell(13, 2, S(10, bold: true, align: CellAlign.Center, v: CellVAlign.Center, wrap: true, borders: "lrtb"), Tpl.Fixed("Khoản")),
            Tpl.Cell(13, 3, S(10, bold: true, align: CellAlign.Center, v: CellVAlign.Center, wrap: true, borders: "lrtb"), Tpl.Fixed("Mục")),
            Tpl.Cell(13, 4, S(10, bold: true, align: CellAlign.Center, v: CellVAlign.Center, wrap: true, borders: "lrtb"), Tpl.Fixed("Tiểu\nmục")),
            Tpl.Cell(13, 5, S(10, bold: true, align: CellAlign.Center, v: CellVAlign.Center, wrap: true, borders: "lrtb"), Tpl.Fixed("Tiết\nmục")),
            Tpl.Cell(13, 6, S(10, bold: true, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrtb"), Tpl.Fixed("Ngành")),
            Tpl.Cell(14, 1, S(11, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrtb"), Tpl.Fixed("A")),
            Tpl.Cell(14, 2, S(11, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrtb"), Tpl.Fixed("B")),
            Tpl.Cell(14, 3, S(11, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrtb"), Tpl.Fixed("C")),
            Tpl.Cell(14, 4, S(11, align: CellAlign.Center, v: CellVAlign.Center, wrap: true, borders: "lrtb"), Tpl.Fixed("D")),
            Tpl.Cell(14, 5, S(11, align: CellAlign.Center, v: CellVAlign.Center, wrap: true, borders: "lrtb"), Tpl.Fixed("E")),
            Tpl.Cell(14, 6, S(11, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrtb"), Tpl.Fixed("F")),
            Tpl.Cell(14, 7, S(11, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrtb"), Tpl.Fixed("G")),
            Tpl.Cell(14, 8, S(11, align: CellAlign.Center, v: CellVAlign.Center, wrap: true, borders: "lrtb"), Tpl.Fixed("1")),
            Tpl.Cell(24, 1, S(12, v: CellVAlign.Center, borders: "lrb")),
            Tpl.Cell(24, 2, S(12, v: CellVAlign.Center, borders: "lrb")),
            Tpl.Cell(24, 3, S(12, v: CellVAlign.Center, borders: "lrb")),
            Tpl.Cell(24, 4, S(12, v: CellVAlign.Center, borders: "lrb")),
            Tpl.Cell(24, 5, S(12, v: CellVAlign.Center, borders: "lrb")),
            Tpl.Cell(24, 6, S(12, v: CellVAlign.Center, borders: "lrb")),
            Tpl.Cell(24, 7, S(11, bold: true, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrb"), Tpl.Fixed("Cộng")),
            Tpl.Cell(24, 8, S(11, bold: true, v: CellVAlign.Center, borders: "lrb", format: "#,##0_);(#,##0)"), Tpl.Computed("Total")),
            Tpl.Merged(25, 1, 1, 2, S(12, bold: true, italic: true, align: CellAlign.Right, v: CellVAlign.Center, borders: "t"), Tpl.Fixed("Bằng chữ: ")),
            Tpl.Cell(25, 3, S(10.5, italic: true, v: CellVAlign.Center, borders: "t"), Tpl.Computed("TotalInWords")),
            Tpl.Cell(25, 4, S(10.5, italic: true, v: CellVAlign.Center, wrap: true, borders: "t")),
            Tpl.Cell(25, 5, S(10.5, italic: true, v: CellVAlign.Center, wrap: true, borders: "t")),
            Tpl.Cell(25, 6, S(10.5, italic: true, v: CellVAlign.Center, wrap: true, borders: "t")),
            Tpl.Cell(25, 7, S(10.5, italic: true, v: CellVAlign.Center, wrap: true, borders: "t")),
            Tpl.Cell(25, 8, S(10.5, italic: true, v: CellVAlign.Center, wrap: true, borders: "t")),
            Tpl.Merged(26, 7, 1, 2, S(13, italic: true, align: CellAlign.Center)),
            Tpl.Merged(27, 1, 1, 6, S(13, bold: true, align: CellAlign.Center)),
            Tpl.Merged(27, 7, 1, 2, S(13, bold: true, align: CellAlign.Center)),
            Tpl.Merged(28, 7, 1, 2, S(13, bold: true, align: CellAlign.Center)),
            Tpl.Merged(32, 1, 1, 6, S(13.5, bold: true, align: CellAlign.Center)),
            Tpl.Merged(32, 7, 1, 2, S(13.5, bold: true, align: CellAlign.Center)),
        },
        Repeat: new RepeatRegion(
            FirstRow: 15, MinRows: 9,
            ProjectRow: new TemplateRow(24.95, new[]
            {
                Tpl.Cell(0, 1, S(10, bold: true, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrtb"), Tpl.Input("LoaiNganSach", "", @default: "010")),
                Tpl.Cell(0, 2, S(10, bold: true, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrtb"), Tpl.Input("Khoan", "", @default: "011")),
                Tpl.Cell(0, 3, S(10, bold: true, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrtb"), Tpl.Input("Muc", "", @default: "9300")),
                Tpl.Cell(0, 4, S(10, bold: true, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrtb"), Tpl.Input("TieuMuc", "", @default: "9301")),
                Tpl.Cell(0, 5, S(10, bold: true, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrtb"), Tpl.Input("TietMuc", "", @default: "00")),
                Tpl.Cell(0, 6, S(10, bold: true, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrtb"), Tpl.Input("Nganh", "", @default: "00")),
                Tpl.Cell(0, 7, S(11, bold: true, align: CellAlign.Center, v: CellVAlign.Center, wrap: true, borders: "lrtb"), Tpl.Computed("Line.Text")),
                Tpl.Cell(0, 8, S(11, bold: true, v: CellVAlign.Center, wrap: true, borders: "lrtb", format: "#,##0_);(#,##0)"), Tpl.Computed("Line.Amount")),
            }),

            ViTriRow: new TemplateRow(24.95, new[]
            {
                Tpl.Cell(0, 1, S(10, bold: true, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrb"), Tpl.Input("LoaiNganSach@line", "")),
                Tpl.Cell(0, 2, S(10, bold: true, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrb"), Tpl.Input("Khoan@line", "")),
                Tpl.Cell(0, 3, S(10, bold: true, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrb"), Tpl.Input("Muc@line", "")),
                Tpl.Cell(0, 4, S(10, bold: true, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrb"), Tpl.Input("TieuMuc@line", "")),
                Tpl.Cell(0, 5, S(10, bold: true, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrb"), Tpl.Input("TietMuc@line", "")),
                Tpl.Cell(0, 6, S(10, bold: true, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrb"), Tpl.Input("Nganh@line", "")),
                Tpl.Cell(0, 7, S(11, bold: true, align: CellAlign.Center, v: CellVAlign.Center, wrap: true, borders: "lrb"), Tpl.Computed("Line.Text")),
                Tpl.Cell(0, 8, S(11, bold: true, v: CellVAlign.Center, wrap: true, borders: "lrb", format: "#,##0_);(#,##0)"), Tpl.Computed("Line.Amount")),
            }),

            DotRow: new TemplateRow(47.25, new[]
            {
                Tpl.Cell(0, 1, S(10, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrb"), Tpl.Input("LoaiNganSach@line", "")),
                Tpl.Cell(0, 2, S(10, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrb"), Tpl.Input("Khoan@line", "")),
                Tpl.Cell(0, 3, S(10, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrb"), Tpl.Input("Muc@line", "")),
                Tpl.Cell(0, 4, S(10, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrb"), Tpl.Input("TieuMuc@line", "")),
                Tpl.Cell(0, 5, S(10, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrb"), Tpl.Input("TietMuc@line", "")),
                Tpl.Cell(0, 6, S(10, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrb"), Tpl.Input("Nganh@line", "")),
                Tpl.Cell(0, 7, S(11, align: CellAlign.Center, v: CellVAlign.Center, wrap: true, borders: "lrb"), Tpl.Computed("Line.Text")),
                Tpl.Cell(0, 8, S(11, v: CellVAlign.Center, wrap: true, borders: "lrb", format: "#,##0_);(#,##0)"), Tpl.Computed("Line.Amount")),
            }),

            BlankRow: new TemplateRow(24.95, new[]
            {
                Tpl.Cell(0, 1, S(10, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrb")),
                Tpl.Cell(0, 2, S(10, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrb")),
                Tpl.Cell(0, 3, S(10, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrb")),
                Tpl.Cell(0, 4, S(10, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrb")),
                Tpl.Cell(0, 5, S(10, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrb")),
                Tpl.Cell(0, 6, S(10, align: CellAlign.Center, v: CellVAlign.Center, borders: "lrb")),
                Tpl.Cell(0, 7, S(11, align: CellAlign.Center, v: CellVAlign.Center, wrap: true, borders: "lrb")),
                Tpl.Cell(0, 8, S(11, v: CellVAlign.Center, wrap: true, borders: "lrb")),
            }))
);
}
