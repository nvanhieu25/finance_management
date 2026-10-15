using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Application.Forms;

/// Một đoạn của một ô biểu mẫu. Ô là DANH SÁCH đoạn vì Mẫu hay nhét nhãn và chỗ điền vào cùng
/// một chữ ("Số ........", " TK……Đơn vị……Số CT……", "Tên CTMT, DA…… Mã số: ……").
public enum SegmentKind
{
    /// Chữ cố định, khoá.
    Fixed,
    /// Ô tự nhập chữ. Lưu theo (dự án, biểu mẫu, khoá).
    Input,
    /// Ô tự nhập số. Lưu dạng số bất biến văn hoá; hiện có dấu chấm phân nghìn.
    InputNumber,
    /// Ô tự nhập chữ nhưng lưu ở Cài đặt, dùng chung mọi dự án và mọi biểu mẫu (tên đơn vị).
    SettingInput,
    /// Ô tự tính từ các builder; khoá.
    Computed,
    /// Ô tự tính, điền sẵn, cho gõ đè; xoá chữ gõ đè thì trở về tự động.
    Overridable
}

public enum CellAlign { General, Left, Center, Right, Justify }

public enum CellVAlign { Bottom, Center, Top }

public enum BorderLine { None, Thin, Hair }

/// Nét kẻ bốn cạnh của một ô (hoặc của cả vùng gộp). Chuỗi cờ: l r t b, chữ thường = nét thường,
/// chữ HOA = nét hair (KS chi dòng 7–17 kẻ trên/dưới bằng nét hair).
public readonly record struct CellBorders(BorderLine Left, BorderLine Right, BorderLine Top, BorderLine Bottom)
{
    public bool Any => Left != BorderLine.None || Right != BorderLine.None
                       || Top != BorderLine.None || Bottom != BorderLine.None;

    public static CellBorders Parse(string flags)
    {
        var (left, right, top, bottom) = (BorderLine.None, BorderLine.None, BorderLine.None, BorderLine.None);
        foreach (var flag in flags)
        {
            var line = char.IsUpper(flag) ? BorderLine.Hair : BorderLine.Thin;
            switch (char.ToLowerInvariant(flag))
            {
                case 'l': left = line; break;
                case 'r': right = line; break;
                case 't': top = line; break;
                case 'b': bottom = line; break;
                default: throw new ArgumentException($"Cờ viền không hợp lệ: '{flag}' trong \"{flags}\".", nameof(flags));
            }
        }
        return new CellBorders(left, right, top, bottom);
    }
}

/// `Text`: với Fixed là chữ in; với các loại còn lại là chữ IN RA khi ô còn trống (chấm chấm của
/// Mẫu, có thể rỗng). `Default`: giá trị điền sẵn khi người dùng chưa gõ gì (không mờ, sửa được).
/// `Hint`: chữ mờ trên MÀN HÌNH, không bao giờ in — chép từ ghi chú của Mẫu. Chuỗi `{năm}` trong
/// Text/Default/Hint được thay bằng năm hiện tại (chữ người dùng gõ thì KHÔNG bao giờ bị thay).
public sealed record TemplateSegment(
    SegmentKind Kind,
    string Text,
    string? Key = null,
    string? Default = null,
    string? Hint = null);

/// `NumberFormat != null` đánh dấu ô số. Ô số không tự khai định dạng thì dùng FormNumberFormat.Default.
public sealed record CellStyle(
    double FontSize,
    bool Bold,
    bool Italic,
    bool Underline,
    CellAlign Align,
    CellVAlign VAlign,
    bool Wrap,
    CellBorders Borders,
    string? NumberFormat);

public sealed record TemplateCell(
    int Row,
    int Col,
    int RowSpan,
    int ColSpan,
    CellStyle Style,
    IReadOnlyList<TemplateSegment> Segments);

/// Một hàng mẫu của vùng lặp. Ô trong hàng có `Row == 0`: chỉ cột (`Col`) có nghĩa, hàng thật
/// do bộ trộn gán.
public sealed record TemplateRow(double Height, IReadOnlyList<TemplateCell> Cells);

/// Vùng lặp của Thông tri: Mẫu dành `MinRows` dòng từ `FirstRow` cho bảng nội dung. Mỗi dòng của
/// bảng là một trong bốn loại hàng mẫu; ít dòng thì phần còn lại là hàng trống, nhiều dòng thì
/// chèn thêm và mọi hàng bên dưới dời xuống.
public sealed record RepeatRegion(
    int FirstRow,
    int MinRows,
    TemplateRow ProjectRow,
    TemplateRow ViTriRow,
    TemplateRow DotRow,
    TemplateRow BlankRow);

public sealed record FormTemplate(
    string SheetName,
    PrintOrientation Orientation,
    IReadOnlyList<double> ColumnWidths,
    IReadOnlyDictionary<int, double> RowHeights,
    IReadOnlyList<TemplateCell> Cells,
    RepeatRegion? Repeat)
{
    /// Mọi đoạn của bản gốc, kể cả các hàng mẫu của vùng lặp.
    public IEnumerable<TemplateSegment> AllSegments()
    {
        var all = Cells.AsEnumerable();
        if (Repeat is { } region)
            all = all.Concat(region.ProjectRow.Cells).Concat(region.ViTriRow.Cells)
                .Concat(region.DotRow.Cells).Concat(region.BlankRow.Cells);
        return all.SelectMany(cell => cell.Segments);
    }
}
