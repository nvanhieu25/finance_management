using ClosedXML.Excel;
using ConstructionFinance.Application.Services.Interfaces;

namespace ConstructionFinance.Application.Forms;

/// Vẽ biểu mẫu ra .xlsx bằng ClosedXML. Chỉ trình bày — mọi con số đã do builder tính xong và mọi bố cục
/// đã nằm ở bản gốc (xem FormExcelExporter.Sheet.cs). Phần này giữ các hằng và cấu hình giấy dùng chung với
/// bộ ghi bản in (PrintWorkbookWriter).
public partial class FormExcelExporter : IFormExcelExporter
{
    internal const string MoneyFormat = "#,##0";
    internal const string FontName = "Times New Roman";
    internal const double FontSize = 13;

    internal static void SetMoney(IXLCell cell, decimal value)
    {
        cell.Value = value;
        cell.Style.NumberFormat.Format = MoneyFormat;
    }

    /// Khổ A4, hướng giấy, lề trái/phải — KHÔNG đụng tới phông. Bộ vẽ từ bản gốc dùng hàm này vì cỡ chữ
    /// của nó đặt từng ô; `ApplyPageDefaults` đặt phông cả sheet và sẽ nghiền mọi cỡ chữ riêng.
    internal static void ApplyPaper(IXLWorksheet ws, XLPageOrientation orientation)
    {
        ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
        ws.PageSetup.PageOrientation = orientation;
        // Cùng MỘT con số với ExcelFitScale.SideMarginInches: bộ dựng MigraDoc tính tỉ lệ co dựa
        // trên lề này, nên hai nơi dùng hai bản sao là một lỗi trôi âm thầm chờ xảy ra.
        ws.PageSetup.Margins.Left = Printing.ExcelFitScale.SideMarginInches;
        ws.PageSetup.Margins.Right = Printing.ExcelFitScale.SideMarginInches;
    }

    internal static void ApplyPageDefaults(IXLWorksheet ws, XLPageOrientation orientation)
    {
        ws.Style.Font.FontName = FontName;
        ws.Style.Font.FontSize = FontSize;
        ApplyPaper(ws, orientation);
    }
}
