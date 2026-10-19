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

    /// Hậu tố khoá của ô tự nhập nằm trong một dòng của vùng lặp: "Muc@line" được lưu riêng cho từng dòng thành "Muc@<khoá dòng>".
    public const string LineScope = "@line";

    public const double DefaultRowHeight = 15;

    /// Khoá lưu trữ của một ô tự nhập; ô thuộc dòng (hậu tố "@line") gắn khoá dòng, dòng không có khoá thì dùng khoá chung.
    public static string StoredKey(string templateKey, ThongTriLine? line) =>
        !templateKey.EndsWith(LineScope, StringComparison.Ordinal) ? templateKey
        : string.IsNullOrEmpty(line?.Key) ? templateKey[..^LineScope.Length]
        : $"{templateKey[..^LineScope.Length]}@{line.Key}";

    /// Ngược lại của StoredKey: khoá lưu trữ → khoá của bản gốc (để biết ô thuộc loại nào).
    public static string TemplateKey(string storedKey)
    {
        var at = storedKey.IndexOf('@');
        return at <= 0 ? storedKey : storedKey[..at] + LineScope;
    }

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
        var regionKeys = new Dictionary<int, string>();   // khoá cỡ hàng của các hàng trong vùng lặp (theo loại hàng mẫu)
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
                regionKeys[row] = line is null ? "blank" : line.Level switch { 0 => "proj", 1 => "vitri", _ => "dot" };
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

        // Khoá cỡ hàng: hàng ngoài vùng lặp theo SỐ HÀNG CỦA MẪU (không dời khi vùng lặp dài ra), hàng trong vùng lặp theo loại
        // hàng mẫu — chỉnh một dòng "đợt" là chỉnh mọi dòng đợt, như chỉnh một kiểu ô.
        var rowKeys = Enumerable.Range(1, rowCount)
            .Select(row => regionKeys.TryGetValue(row, out var kind) ? kind : $"r{(row >= regionEnd ? row - shift : row)}")
            .ToList();

        // Cỡ ô người dùng đã kéo (nếu có) đè lên cỡ của Mẫu. Mặc định (chưa kéo) là đúng Mẫu.
        var columnWidths = template.ColumnWidths
            .Select((width, index) => FormLayoutKeys.TryGetWidth(inputs, index + 1, out var custom) ? custom : width)
            .ToList();
        for (var i = 0; i < rowHeights.Count; i++)
            if (FormLayoutKeys.TryGetHeight(inputs, rowKeys[i], out var customHeight))
                rowHeights[i] = customHeight;

        var ordered = cells.OrderBy(c => c.Row).ThenBy(c => c.Col).ToList();
        var overflowed = WithOverflow(ordered, columnWidths.Count);
        return new ResolvedSheet(
            template.SheetName, template.Orientation, columnWidths, rowHeights,
            WithFreeInputs(overflowed, rowKeys, template.Repeat?.FirstRow ?? 0, columnWidths, context), rowKeys);
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
            {
                var key = StoredKey(segment.Key!, line);
                return ResolveText(segment, key, context.Inputs.Get(key) ?? Year(segment.Default, context), context);
            }
            case SegmentKind.SettingInput:
                return ResolveText(segment, segment.Key!, context.Org.Get(segment.Key!), context);
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
    private static ResolvedSegment ResolveText(TemplateSegment segment, string key, string? value, Context context)
    {
        var hint = Year(segment.Hint, context);
        if (!string.IsNullOrEmpty(value))
            return new ResolvedSegment(segment.Kind, key, value, value, hint, false, false, null);

        var placeholder = Year(segment.Text, context)!;
        return new ResolvedSegment(
            // Ô điền sẵn mà người dùng đã xoá hết: chữ mờ nhắc lại giá trị Mẫu, để ô trống không biến thành một dải không nhìn thấy.
            segment.Kind, key, placeholder, "",
            hint ?? (placeholder.Length > 0 ? placeholder : Year(segment.Default, context)),
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

    // ===== ô trống có viền gõ tự do =====

    /// Khoá của ô gõ tự do (xem WithFreeInputs).
    public const string FreePrefix = "x.";

    public static bool IsFreeKey(string key) => key.StartsWith(FreePrefix, StringComparison.Ordinal);

    /// Như Excel: ô trống nằm trong bảng (có viền, không có đoạn nào, không bị chữ ô khác tràn lên) cho gõ chữ tuỳ ý. Chữ
    /// gõ vào in ra và xuất Excel đúng như vậy; không tham gia tính toán. Khoá theo khoá hàng + cột, hàng trống của vùng
    /// lặp theo thứ tự trong vùng.
    private static IReadOnlyList<ResolvedCell> WithFreeInputs(
        IReadOnlyList<ResolvedCell> cells, IReadOnlyList<string> rowKeys, int regionFirstRow,
        IReadOnlyList<double> columnWidths, Context context)
    {
        // Ô bị chữ tràn che: chữ khoá/tự tính che hết dải tràn; ô nhập tràn (" + Mục …, tiểu mục …") chỉ che đúng phần
        // chữ của nó chiếm (ước lượng theo ký tự), phần dải còn lại bỏ trống — các ô số bên cạnh vẫn phải gõ được.
        var covered = new HashSet<(int Row, int Col)>();
        foreach (var cell in cells.Where(c => c.TextColSpan > c.ColSpan))
        {
            var natural = cell.IsEditable ? NaturalChars(cell) : double.MaxValue;
            var used = Enumerable.Range(cell.Col, cell.ColSpan).Sum(col => columnWidths[col - 1]);
            for (var col = cell.Col + cell.ColSpan; col < cell.Col + cell.TextColSpan && used < natural; col++)
            {
                covered.Add((cell.Row, col));
                used += columnWidths[col - 1];
            }
        }

        return cells.Select(cell =>
        {
            if (cell.Segments.Count != 0 || !cell.Style.Borders.Any || covered.Contains((cell.Row, cell.Col)))
                return cell;

            var rowKey = rowKeys[cell.Row - 1];
            if (rowKey == "blank") rowKey = $"blank{cell.Row - regionFirstRow}";
            var key = $"{FreePrefix}{rowKey}.{cell.Col}";
            var value = context.Inputs.Get(key) ?? "";
            var segment = new ResolvedSegment(SegmentKind.Input, key, value, value, null, value.Length == 0, false, null);
            return cell with { Segments = new[] { segment } };
        }).ToList();
    }

    /// Ước lượng bề rộng chữ của ô theo "ký tự Excel": chấm chấm hẹp hơn chữ thường nhiều.
    private static double NaturalChars(ResolvedCell cell) => cell.Segments.Sum(segment =>
    {
        var sample = segment.Text.Length >= (segment.Hint?.Length ?? 0) ? segment.Text : segment.Hint ?? "";
        return Math.Max(4, sample.Sum(ch => ch is '.' or '…' or ' ' ? 0.35 : 1.0));
    });

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
