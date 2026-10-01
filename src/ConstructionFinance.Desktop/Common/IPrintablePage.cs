using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Desktop.Common;

/// Một màn hình khai báo "tôi in ra cái gì" bằng model thuần (PrintSheet). Màn hình không biết
/// gì về .xlsx — việc ghi tệp là của PrintWorkbookWriter, nên tầng Desktop không đụng tới thư viện
/// ghi Excel.
public interface IPrintablePage
{
    /// Null nghĩa là màn này không in được — nút In mờ đi.
    string? PrintSheetName { get; }

    /// Nội dung sẽ in, đúng như đang hiện trên màn hình. Null khi chưa có gì để in — kể cả khi cả màn
    /// không in được. Một màn có tên sheet vẫn được phép trả null ở đây (vd. biểu mẫu chưa chọn gói thầu).
    PrintSheet? BuildPrintSheet();
}

/// Một workbook gồm nhiều màn: "In toàn bộ" gom sheet của mọi màn in được vào một tệp.
public interface IPrintableWorkbook
{
    /// Tên cả workbook sẽ in, tức tên dự án đang chọn.
    string PrintWorkbookName { get; }

    IReadOnlyList<PrintSheet> BuildAllSheets();
}
