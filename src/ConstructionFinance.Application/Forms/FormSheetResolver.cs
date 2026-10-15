using System.Globalization;

namespace ConstructionFinance.Application.Forms;

/// Trộn bản gốc với số đã tính, chữ người dùng đã gõ và tên đơn vị thành một ResolvedSheet.
/// Thuần: không biết gì về Excel, Avalonia hay MigraDoc.
public static class FormSheetResolver
{
    public const string YearToken = "{năm}";

    /// Khoá dành riêng của các hàng mẫu ở vùng lặp: chữ và số tiền của dòng đang vẽ.
    public const string LineText = "Line.Text";
    public const string LineAmount = "Line.Amount";

    public const double DefaultRowHeight = 15;

    private sealed record Context(
        FormTemplate Template, FormValues Computed, FormInputSet Inputs, FormOrgInfo Org, string Year);

    public static ResolvedSheet Resolve(
        FormTemplate template,
        FormValues computed,
        FormInputSet inputs,
        FormOrgInfo org,
        int year,
        IReadOnlyList<ThongTriLine>? lines = null)
    {
        var context = new Context(template, computed, inputs, org, year.ToString(CultureInfo.InvariantCulture));
        var cells = new List<ResolvedCell>();
        var heights = new Dictionary<int, double>();
        var shift = 0;
        var regionEnd = int.MaxValue;

        if (template.Repeat is { } region)
        {
            var regionLines = lines ?? Array.Empty<ThongTriLine>();
            var count = Math.Max(region.MinRows, regionLines.Count);
            shift = count - region.MinRows;
            regionEnd = region.FirstRow + region.MinRows;

            for (var i = 0; i < count; i++)
            {
                var row = region.FirstRow + i;
                var line = i < regionLines.Count ? regionLines[i] : null;
                var prototype = line is null
                    ? region.BlankRow
                    : line.Level switch { 0 => region.ProjectRow, 1 => region.ViTriRow, _ => region.DotRow };

                heights[row] = prototype.Height;
                foreach (var cell in prototype.Cells)
                    cells.Add(ResolveCell(cell with { Row = row }, context, line));
            }
        }

        foreach (var cell in template.Cells)
        {
            var row = cell.Row >= regionEnd ? cell.Row + shift : cell.Row;
            cells.Add(ResolveCell(cell with { Row = row }, context, null));
        }

        foreach (var (row, height) in template.RowHeights)
            heights[row >= regionEnd ? row + shift : row] = height;

        var rowCount = Math.Max(
            cells.Count == 0 ? 0 : cells.Max(c => c.Row + c.RowSpan - 1),
            heights.Count == 0 ? 0 : heights.Keys.Max());
        var rowHeights = Enumerable.Range(1, rowCount)
            .Select(row => heights.TryGetValue(row, out var h) ? h : DefaultRowHeight)
            .ToList();

        var ordered = cells.OrderBy(c => c.Row).ThenBy(c => c.Col).ToList();
        return new ResolvedSheet(
            template.SheetName, template.Orientation, template.ColumnWidths, rowHeights,
            WithOverflow(ordered, template.ColumnWidths.Count));
    }

    private static ResolvedCell ResolveCell(TemplateCell cell, Context context, ThongTriLine? line) =>
        new(cell.Row, cell.Col, cell.RowSpan, cell.ColSpan, cell.ColSpan, cell.Style,
            cell.Segments.Select(segment => ResolveSegment(segment, cell.Style, context, line)).ToList());

    private static ResolvedSegment ResolveSegment(
        TemplateSegment segment, CellStyle style, Context context, ThongTriLine? line)
    {
        switch (segment.Kind)
        {
            case SegmentKind.Fixed:
            {
                var text = Year(segment.Text, context)!;
                return new ResolvedSegment(segment.Kind, null, text, text, null, false, false, null);
            }
            case SegmentKind.Input:
                return ResolveText(segment, context.Inputs.Get(segment.Key!) ?? Year(segment.Default, context), context);
            case SegmentKind.SettingInput:
                return ResolveText(segment, context.Org.Get(segment.Key!), context);
            case SegmentKind.InputNumber:
                return context.Inputs.TryGetNumber(segment.Key!, out var typed)
                    ? Numeric(segment, typed, style, overridden: false)
                    : new ResolvedSegment(segment.Kind, segment.Key, "", "", Year(segment.Hint, context), false, false, null);
            case SegmentKind.Computed:
            {
                var value = Lookup(segment.Key!, context, line);
                return value.Number is { } number
                    ? Numeric(segment, number, style, overridden: false)
                    : Plain(segment, value.Text ?? "", overridden: false, context);
            }
            case SegmentKind.Overridable:
            {
                var value = Lookup(segment.Key!, context, line);
                var stored = context.Inputs.Get(segment.Key!);
                if (value.Number is { } computedNumber)
                    return !string.IsNullOrEmpty(stored) && context.Inputs.TryGetNumber(segment.Key!, out var overriddenNumber)
                        ? Numeric(segment, overriddenNumber, style, overridden: true)
                        : Numeric(segment, computedNumber, style, overridden: false);

                return !string.IsNullOrEmpty(stored)
                    ? Plain(segment, stored, overridden: true, context)
                    : Plain(segment, value.Text ?? "", overridden: false, context);
            }
            default:
                throw new NotSupportedException($"Loại đoạn chưa hỗ trợ: {segment.Kind}");
        }
    }

    /// Ô chữ tự nhập: có chữ (đã gõ hoặc điền sẵn) thì in chữ đó; trống thì in chữ chấm chấm của Mẫu và
    /// cho màn hình một Hint mờ (ghi chú của Mẫu, nếu không có thì chính chữ chấm chấm).
    private static ResolvedSegment ResolveText(TemplateSegment segment, string? value, Context context)
    {
        var hint = Year(segment.Hint, context);
        if (!string.IsNullOrEmpty(value))
            return new ResolvedSegment(segment.Kind, segment.Key, value, value, hint, false, false, null);

        var placeholder = Year(segment.Text, context)!;
        return new ResolvedSegment(
            segment.Kind, segment.Key, placeholder, "", hint ?? (placeholder.Length > 0 ? placeholder : null),
            IsPlaceholder: true, false, null);
    }

    private static ResolvedSegment Plain(TemplateSegment segment, string text, bool overridden, Context context) =>
        new(segment.Kind, segment.Key, text, text, Year(segment.Hint, context), false, overridden, null);

    private static ResolvedSegment Numeric(TemplateSegment segment, decimal number, CellStyle style, bool overridden) =>
        new(segment.Kind, segment.Key, FormNumberFormat.Format(number, style.NumberFormat),
            FormNumberFormat.Group(number), segment.Hint, false, overridden, number);

    private static ComputedValue Lookup(string key, Context context, ThongTriLine? line)
    {
        if (key == LineText) return new ComputedValue(line?.Content ?? "", null);
        if (key == LineAmount) return line is null ? new ComputedValue("", null) : new ComputedValue(null, line.Amount);
        if (context.Computed.TryGet(key, out var value)) return value;
        if (context.Computed.BlankMissing) return new ComputedValue("", null);

        throw new InvalidOperationException(
            $"Biểu mẫu \"{context.Template.SheetName}\" cần giá trị cho khoá tự tính \"{key}\" nhưng không được cấp.");
    }

    private static string? Year(string? text, Context context) => text?.Replace(YearToken, context.Year);

    // ===== chữ tràn sang ô trống bên phải =====

    /// Excel cho chữ canh trái (hoặc mặc định) của ô KHÔNG gộp, KHÔNG xuống dòng, tràn sang các ô trống bên
    /// phải. Tính MỘT lần ở đây để màn hình và bản in cùng đọc.
    private static IReadOnlyList<ResolvedCell> WithOverflow(List<ResolvedCell> cells, int columnCount)
    {
        var occupied = new HashSet<(int Row, int Col)>();
        foreach (var cell in cells)
        {
            var merged = cell.RowSpan > 1 || cell.ColSpan > 1;
            for (var row = cell.Row; row < cell.Row + cell.RowSpan; row++)
            for (var col = cell.Col; col < cell.Col + cell.ColSpan; col++)
                if (merged || cell.DisplayText.Length > 0 || cell.IsEditable)
                    occupied.Add((row, col));
        }

        return cells.Select(cell =>
        {
            if (!CanOverflow(cell)) return cell;
            var span = 1;
            while (cell.Col + span <= columnCount && !occupied.Contains((cell.Row, cell.Col + span))) span++;
            return span > 1 ? cell with { TextColSpan = span } : cell;
        }).ToList();
    }

    private static bool CanOverflow(ResolvedCell cell) =>
        cell.RowSpan == 1 && cell.ColSpan == 1 && !cell.Style.Wrap
        && (cell.Style.Align == CellAlign.General || cell.Style.Align == CellAlign.Left)
        && (cell.DisplayText.Length > 0 || cell.IsEditable)
        && cell.Segments.All(segment => segment.Number is null);
}
