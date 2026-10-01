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

/// Một dòng. IndentLevel thụt ô đầu tiên theo bậc (Nhóm 0 · Gói 1 · Đợt 2).
public sealed record PrintRow(
    IReadOnlyList<PrintCell> Cells,
    int IndentLevel = 0,
    bool IsBold = false,
    bool IsShaded = false);

public abstract record PrintSheet(string SheetName, PrintPageProfile Page);

/// Sheet dạng bảng: Sổ Vị trí, Tổng hợp, Quyết toán năm. Bộ ghi đặt dòng 1 là tiêu đề cột
/// (lấy từ Columns, in đậm) và Rows bắt đầu từ dòng 2. Page.RepeatRowsTop quyết định bao
/// nhiêu dòng đầu được lặp lại ở mỗi trang, đếm từ dòng 1 — nên RepeatRowsTop = 2 là lặp
/// tiêu đề cộng dòng dữ liệu đầu tiên. Kiểu từng dòng dữ liệu do PrintRow.IsBold /
/// IsShaded quyết định.
public sealed record PrintTableSheet(
    string SheetName,
    PrintPageProfile Page,
    IReadOnlyList<PrintColumn> Columns,
    IReadOnlyList<PrintRow> Rows) : PrintSheet(SheetName, Page);

/// Sheet biểu mẫu: dùng lại nguyên bộ ghi trong FormExcelExporter.
/// Đúng MỘT trong ba thuộc tính được khác null.
public sealed record FormPayload(
    ThongTriForm? ThongTri = null,
    KsChiForm? KsChi = null,
    GdnttForm? Gdntt = null);

public sealed record PrintFormSheet(
    string SheetName,
    PrintPageProfile Page,
    FormPayload Form) : PrintSheet(SheetName, Page);
