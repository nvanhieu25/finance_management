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
/// Group: tiêu đề tầng trên, gộp ngang qua các cột LIỀN NHAU cùng Group (như Mẫu: "Thanh toán khối lượng
/// hoàn thành" phủ ba cột). Có cột nào mang Group thì tiêu đề thành hai tầng; cột không Group gộp dọc cả hai tầng.
public sealed record PrintColumn(string Header, double WidthChars, PrintAlign Align, string? Group = null);

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
///
/// Titles (tuỳ chọn): các dòng tiêu đề in đậm, căn giữa, phía TRÊN bảng (dòng đầu to hơn), cách bảng một dòng trống.
/// Có Titles hoặc tiêu đề hai tầng thì bảng dời xuống tương ứng; RepeatRowsTop vẫn đếm từ đầu phần tiêu đề CỘT
/// (tiêu đề cột hai tầng tính là một), không đếm Titles.
public sealed record PrintTableSheet(
    string SheetName,
    PrintPageProfile Page,
    IReadOnlyList<PrintColumn> Columns,
    IReadOnlyList<PrintRow> Rows,
    IReadOnlyList<string>? Titles = null) : PrintSheet(SheetName, Page)
{
    /// Số dòng mà Titles chiếm trên sheet, kể cả dòng trống ngăn cách.
    public int TitleRowCount => Titles is { Count: > 0 } titles ? titles.Count + 1 : 0;

    /// 2 khi có cột mang Group, ngược lại 1.
    public int HeaderRowCount => Columns.Any(column => column.Group is not null) ? 2 : 1;

    /// Các dải cột liền nhau cùng Group: (cột đầu, số cột, chữ). Cột không Group không có trong danh sách.
    public IReadOnlyList<(int First, int Count, string Text)> GroupSpans()
    {
        var spans = new List<(int, int, string)>();
        for (var i = 0; i < Columns.Count;)
        {
            var group = Columns[i].Group;
            var end = i + 1;
            while (group is not null && end < Columns.Count && Columns[end].Group == group) end++;
            if (group is not null) spans.Add((i, end - i, group));
            i = end;
        }
        return spans;
    }
}

/// Sheet biểu mẫu dựng từ bản gốc: bộ ghi .xlsx, bộ dựng MigraDoc và màn hình cùng đọc MỘT ResolvedSheet,
/// nên thêm biểu mẫu mới không cần thêm record nào ở đây.
public sealed record FormTemplatePrintSheet(
    string SheetName,
    PrintPageProfile Page,
    ResolvedSheet Sheet) : PrintSheet(SheetName, Page);
