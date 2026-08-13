using ClosedXML.Excel;
using ConstructionFinance.Application.Services.Interfaces;

namespace ConstructionFinance.Application.Forms;

/// Vẽ ba biểu mẫu ra .xlsx bằng ClosedXML. Chỉ trình bày — mọi con số đã do
/// builder tính xong. Bố cục dựng lại theo reference/Mẫu.xlsx.
public partial class FormExcelExporter : IFormExcelExporter
{
    internal const string MoneyFormat = "#,##0";
    internal const string FontName = "Times New Roman";
    internal const double FontSize = 13;

    public Task ExportThongTriAsync(ThongTriForm form, string targetPath, CancellationToken ct = default) =>
        Task.Run(() => WriteThongTri(form, targetPath), ct);

    // TODO(Task 8): cài sheet KS chi và GĐNTT trong một file partial khác của lớp này.
    public Task ExportKsChiAsync(KsChiForm form, string targetPath, CancellationToken ct = default) =>
        throw new NotImplementedException("Sheet KS chi sẽ được Task 8 cài đặt.");

    public Task ExportGdnttAsync(GdnttForm form, string targetPath, CancellationToken ct = default) =>
        throw new NotImplementedException("Sheet GĐNTT sẽ được Task 8 cài đặt.");

    private static void WriteThongTri(ThongTriForm form, string targetPath)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Thông tri");
        ApplyPageDefaults(ws, XLPageOrientation.Portrait);

        ws.Cell(1, 1).Value = form.DonViCapTren ?? "";
        ws.Cell(1, 7).Value = "Mẫu số CQ06-HD";
        ws.Cell(2, 1).Value = form.DonViCoSo ?? "";
        ws.Cell(3, 1).Value = $"Số      {form.SoThongTri ?? "............."}";
        ws.Cell(3, 7).Value = $"Ghi sổ ngày {form.NgayGhiSo ?? "     tháng     năm"}";

        ws.Cell(4, 1).Value = "THÔNG TRI";
        ws.Cell(5, 1).Value = "Thu hồi tạm ứng";
        ws.Cell(6, 1).Value = "Kinh phí: NSNN";
        foreach (var r in new[] { 4, 5, 6 })
        {
            ws.Range(r, 1, r, 8).Merge();
            ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        ws.Cell(4, 1).Style.Font.Bold = true;

        ws.Cell(8, 1).Value = "Đơn vị:";
        ws.Cell(8, 7).Value = $" TK{Blank(form.TaiKhoan1)}  Đơn vị{Blank(form.DonVi1)}  Số CT{Blank(form.SoChungTu1)}";
        ws.Cell(9, 1).Value = $"Về {form.VeThangNam ?? "tháng      năm"}";
        ws.Cell(9, 7).Value = $" TK{Blank(form.TaiKhoan2)}  Đơn vị{Blank(form.DonVi2)}  Số CT{Blank(form.SoChungTu2)}";
        ws.Cell(10, 1).Value =
            $"Tên CTMT, DA: {form.TenCTMT ?? "................."}    Mã số: {form.MaSo ?? "............."}";
        ws.Cell(11, 7).Value = "Đơn vị tính: Đồng";

        // Đầu bảng: hai dòng 12–13 gộp dọc, dòng 14 là dòng chữ cái A..G như Mẫu.
        ws.Cell(12, 1).Value = "Loại ngân sách";
        ws.Range(12, 1, 13, 1).Merge();
        ws.Cell(12, 2).Value = "Mục lục ngân sách";
        ws.Range(12, 2, 12, 6).Merge();
        ws.Cell(13, 2).Value = "Khoản";
        ws.Cell(13, 3).Value = "Mục";
        ws.Cell(13, 4).Value = "Tiểu mục";
        ws.Cell(13, 5).Value = "Tiết mục";
        ws.Cell(13, 6).Value = "Ngành";
        ws.Cell(12, 7).Value = "NỘI DUNG";
        ws.Range(12, 7, 13, 7).Merge();
        ws.Cell(12, 8).Value = "SỐ TIỀN";
        ws.Range(12, 8, 13, 8).Merge();
        ws.Range(12, 1, 13, 8).Style.Font.Bold = true;
        ws.Range(12, 1, 13, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var letters = new[] { "A", "B", "C", "D", "E", "F", "G", "1" };
        for (var c = 0; c < letters.Length; c++)
        {
            ws.Cell(14, c + 1).Value = letters[c];
            ws.Cell(14, c + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Khối Mục lục ngân sách nằm trên dòng nội dung đầu tiên (dòng 15), như Mẫu.
        ws.Cell(15, 1).Value = form.LoaiNganSach ?? "";
        ws.Cell(15, 2).Value = form.Khoan ?? "";
        ws.Cell(15, 3).Value = form.Muc ?? "";
        ws.Cell(15, 4).Value = form.TieuMuc ?? "";
        ws.Cell(15, 5).Value = form.TietMuc ?? "";
        ws.Cell(15, 6).Value = form.Nganh ?? "";

        var row = 15;
        foreach (var line in form.Lines)
        {
            // Thụt đầu dòng bằng Indent của Excel, KHÔNG chèn dấu cách vào chuỗi —
            // để nội dung ô còn so khớp được và người dùng copy ra không dính khoảng trắng.
            ws.Cell(row, 7).Value = line.Content;
            ws.Cell(row, 7).Style.Alignment.Indent = line.Level;
            SetMoney(ws.Cell(row, 8), line.Amount);
            if (line.Level == 0) ws.Row(row).Style.Font.Bold = true;
            row++;
        }

        ws.Cell(row, 7).Value = "Cộng";
        ws.Cell(row, 7).Style.Font.Bold = true;
        SetMoney(ws.Cell(row, 8), form.Total);
        ws.Cell(row, 8).Style.Font.Bold = true;

        ws.Range(12, 1, row, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(12, 1, row, 8).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        row++;
        ws.Cell(row, 1).Value = $"Bằng chữ: {form.TotalInWords}";
        ws.Range(row, 1, row, 8).Merge();

        ws.Columns(1, 6).Width = 9;
        ws.Column(7).Width = 52;
        ws.Column(8).Width = 18;
        wb.SaveAs(targetPath);
    }

    /// Ô thủ tục người dùng bỏ trống thì in dấu chấm cho họ điền tay như Mẫu.
    internal static string Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "…………" : value!;

    internal static void SetMoney(IXLCell cell, decimal value)
    {
        cell.Value = value;
        cell.Style.NumberFormat.Format = MoneyFormat;
    }

    internal static void ApplyPageDefaults(IXLWorksheet ws, XLPageOrientation orientation)
    {
        ws.Style.Font.FontName = FontName;
        ws.Style.Font.FontSize = FontSize;
        ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
        ws.PageSetup.PageOrientation = orientation;
        ws.PageSetup.Margins.Left = 0.5;
        ws.PageSetup.Margins.Right = 0.5;
    }
}
