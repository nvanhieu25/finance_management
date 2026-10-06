using ConstructionFinance.Application.Forms;

namespace ConstructionFinance.Application.Printing;

/// Kiểu giá trị của một ô in. Quyết định bộ ghi đặt định dạng số nào — bản in phải
/// cộng được trong Excel, nên tiền và ngày đi vào tệp dưới dạng số/ngày thật chứ
/// không phải chuỗi đã định dạng sẵn.
public enum PrintCellKind { Text, Money, Date, Number }

public enum PrintAlign { Left, Center, Right }

public sealed record PrintCell(PrintCellKind Kind, string? Text, decimal? Number, DateOnly? Date)
{
    public static PrintCell Empty { get; } = new(PrintCellKind.Text, null, null, null);

    public static PrintCell OfText(string? value) => new(PrintCellKind.Text, value, null, null);

    /// Tiền bằng 0 thành ô TRỐNG — đúng quy ước MoneyText.Format(0m) trên màn hình.
    public static PrintCell OfMoney(decimal value) =>
        value == 0m ? Empty : new PrintCell(PrintCellKind.Money, null, value, null);

    public static PrintCell OfNumber(decimal value) => new(PrintCellKind.Number, null, value, null);

    public static PrintCell OfDate(DateOnly? value) =>
        value is null ? Empty : new PrintCell(PrintCellKind.Date, null, null, value);
}

/// Một cột của sheet dạng bảng. WidthChars tính theo KÝ TỰ (đơn vị của Excel),
/// không phải pixel — cố tình không lấy từ LedgerColumnWidths của màn hình.
public sealed record PrintColumn(string Header, double WidthChars, PrintAlign Align);

/// Nền của một dòng. Hai bậc xám để dòng TỔNG CỘNG (Light) không trùng màu với dòng Nhóm (VeryLight):
/// TỔNG CỘNG là dòng người đọc tìm đầu tiên và cũng là dòng được lặp ở đầu mỗi trang
/// (PrintPageProfile.RepeatRowsTop), nên nó phải nổi hơn dòng Nhóm.
public enum PrintShade { None, Light, VeryLight }

/// Một dòng. IndentLevel thụt ô chữ đầu tiên của dòng nằm ở cột căn trái, theo bậc (Nhóm 0 · Gói 1 ·
/// Đợt 2); dòng không có ô nào như vậy thì không thụt gì.
public sealed record PrintRow(
    IReadOnlyList<PrintCell> Cells,
    int IndentLevel = 0,
    bool IsBold = false,
    PrintShade Shade = PrintShade.None);

public abstract record PrintSheet(string SheetName, PrintPageProfile Page);

/// Sheet dạng bảng: Sổ Vị trí, Tổng hợp, Quyết toán năm. Bộ ghi đặt dòng 1 là tiêu đề cột
/// (lấy từ Columns, in đậm) và Rows bắt đầu từ dòng 2. Page.RepeatRowsTop quyết định bao
/// nhiêu dòng đầu được lặp lại ở mỗi trang, đếm từ dòng 1 — nên RepeatRowsTop = 2 là lặp
/// tiêu đề cộng dòng dữ liệu đầu tiên. Kiểu từng dòng dữ liệu do PrintRow.IsBold /
/// Shade quyết định.
public sealed record PrintTableSheet(
    string SheetName,
    PrintPageProfile Page,
    IReadOnlyList<PrintColumn> Columns,
    IReadOnlyList<PrintRow> Rows) : PrintSheet(SheetName, Page);

/// Sheet biểu mẫu: dùng lại nguyên bộ ghi trong FormExcelExporter. Mỗi biểu mẫu một record
/// con, nên KIỂU của sheet chính là biểu mẫu nó mang: không còn biểu diễn được trạng thái
/// "không mang biểu mẫu nào" hay "mang hai biểu mẫu", và bộ ghi không cần guard lúc chạy cho
/// hai trạng thái đó nữa. Thêm biểu mẫu mới (KH vốn / Vốn / QĐ...) là thêm một record con ở
/// đây và một nhánh trong PrintWorkbookWriter.WriteForm — test
/// PrintFormSheet_chi_co_ba_bieu_mau_ma_bo_ghi_biet_ve đỏ để nhắc.
public abstract record PrintFormSheet(string SheetName, PrintPageProfile Page)
    : PrintSheet(SheetName, Page);

public sealed record ThongTriPrintSheet(
    string SheetName,
    PrintPageProfile Page,
    ThongTriForm Form) : PrintFormSheet(SheetName, Page);

public sealed record KsChiPrintSheet(
    string SheetName,
    PrintPageProfile Page,
    KsChiForm Form) : PrintFormSheet(SheetName, Page);

public sealed record GdnttPrintSheet(
    string SheetName,
    PrintPageProfile Page,
    GdnttForm Form) : PrintFormSheet(SheetName, Page);
