using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Application.Forms;

/// Một đoạn đã trộn. `Text` là chữ IN RA (đã thay năm, đã điền chữ chấm chấm khi ô trống);
/// `Value` là chữ trong ô nhập trên màn hình (rỗng khi chưa gõ); `Hint` là chữ mờ trên màn hình.
public sealed record ResolvedSegment(
    SegmentKind Kind,
    string? Key,
    string Text,
    string Value,
    string? Hint,
    bool IsPlaceholder,
    bool IsOverridden,
    decimal? Number);

/// `TextColSpan >= ColSpan`: số cột mà CHỮ được tràn sang (Excel cho chữ trái/thường tràn sang ô
/// trống bên phải). Đường kẻ của ô vẫn chỉ nằm trong `ColSpan`.
public sealed record ResolvedCell(
    int Row,
    int Col,
    int RowSpan,
    int ColSpan,
    int TextColSpan,
    CellStyle Style,
    IReadOnlyList<ResolvedSegment> Segments)
{
    public string DisplayText => string.Concat(Segments.Select(segment => segment.Text));

    public bool IsEditable => Segments.Any(segment =>
        segment.Kind is SegmentKind.Input or SegmentKind.InputNumber
            or SegmentKind.SettingInput or SegmentKind.Overridable);

    public bool IsComputed => Segments.Any(segment =>
        segment.Kind is SegmentKind.Computed or SegmentKind.Overridable);
}

/// Tờ biểu mẫu đã trộn xong — thứ DUY NHẤT mà ba bộ vẽ (màn hình, Excel, in) đọc.
/// `RowHeights[i]` là chiều cao hàng i+1 (pt); `ColumnWidths[i]` là bề rộng cột i+1 (ký tự Excel).
public sealed record ResolvedSheet(
    string SheetName,
    PrintOrientation Orientation,
    IReadOnlyList<double> ColumnWidths,
    IReadOnlyList<double> RowHeights,
    IReadOnlyList<ResolvedCell> Cells)
{
    public int RowCount => RowHeights.Count;
    public int ColumnCount => ColumnWidths.Count;
}
