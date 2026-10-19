using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ConstructionFinance.Application.Forms;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

/// So thẳng ba bản gốc với reference/Mẫu.xlsx. Đỏ khi ai sửa bản gốc lệch khỏi Mẫu, VÀ khi Mẫu đổi
/// mà bản gốc không theo. Đọc Mẫu bằng ClosedXML — khác thư viện với script sinh bản gốc (openpyxl),
/// nên hai bên không cùng sai một kiểu.
///
/// Chỉ bỏ qua đúng các khác biệt đã ghi ở plan §"Chỗ plan chốt thêm": chữ ghi chú không in (Hint),
/// năm hiện tại ({năm} ↔ 2026), vùng lặp của Thông tri, ô công thức (so vị trí+loại, không so chữ),
/// hai gộp thêm của GĐNTT, cột/ô ngoài vùng biểu mẫu (GĐNTT cột J–M).
public class TemplateMatchesMauTests
{
    private sealed record Sheet(string MauName, Func<FormTemplate> Create, int LastRow, int LastCol);

    private static readonly Dictionary<string, Sheet> Sheets = new()
    {
        ["Thông tri"] = new("Thông tri", ThongTriTemplate.Create, LastRow: 33, LastCol: 8),
        ["KS chi"] = new("KS chi", KsChiTemplate.Create, LastRow: 34, LastCol: 5),
        ["GĐNTT"] = new("GĐNTT", GdnttTemplate.Create, LastRow: 36, LastCol: 9),
    };

    /// Gộp chỉ có ở bản gốc (cột H của Mẫu rộng 0,86 nên số đặt ở H13/H25 chỉ ra ###).
    private static readonly Dictionary<string, string[]> ExtraMerges = new()
    {
        ["GĐNTT"] = new[] { "H13:I13", "H25:I25", "D28:I28" },
    };

    /// Mẫu ghi thẳng CHỮ `="Vị trí 1"!D9` (không bao giờ tính ra) vào GĐNTT!D28: lỗi của Mẫu.
    private static readonly HashSet<string> LiteralTextWhereMauHasBug = new() { "GĐNTT!D28" };

    /// Ghi chú nội bộ của người soạn Mẫu, không phải nội dung biểu mẫu.
    private static readonly HashSet<string> MauNotes = new() { "GĐNTT!M13" };

    private const int RegionFirstRow = 15;
    private const int RegionRows = 9;
    private const int RegionProjectRow = 15, RegionViTriRow = 16, RegionDotRow = 17, RegionBlankRow = 20;

    private static readonly Lazy<XLWorkbook> Mau = new(() => new XLWorkbook(FindMau()));

    private static string FindMau()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "reference", "Mẫu.xlsx");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("Không thấy reference/Mẫu.xlsx — tệp này phải nằm trong repo.");
    }

    public static IEnumerable<object[]> SheetNames() => Sheets.Keys.Select(name => new object[] { name });

    private static string Nfc(string? text) => (text ?? "").Normalize(NormalizationForm.FormC);

    private static string Squash(string text) => Regex.Replace(Nfc(text), @"\s+", " ").Trim();

    private static string Where(string sheet, int row, int col) => $"{sheet}!{XLHelper.GetColumnLetterFromNumber(col)}{row}";

    private static void ShouldHaveNoProblems(List<string> problems) =>
        Assert.True(problems.Count == 0, "\n" + string.Join("\n", problems));

    // ===== khổ giấy, cột, hàng =====

    [Theory]
    [MemberData(nameof(SheetNames))]
    public void Paper_and_orientation_match(string name)
    {
        var template = Sheets[name].Create();
        var ws = Mau.Value.Worksheet(name);

        ws.PageSetup.PaperSize.ShouldBe(XLPaperSize.A4Paper);
        ws.PageSetup.PageOrientation.ShouldBe(XLPageOrientation.Portrait);
        template.Orientation.ShouldBe(ConstructionFinance.Application.Printing.PrintOrientation.Portrait);
        template.SheetName.ShouldBe(name);
    }

    [Theory]
    [MemberData(nameof(SheetNames))]
    public void Column_widths_match(string name)
    {
        var sheet = Sheets[name];
        var template = sheet.Create();
        var ws = Mau.Value.Worksheet(name);
        var problems = new List<string>();

        template.ColumnWidths.Count.ShouldBe(sheet.LastCol);
        for (var col = 1; col <= sheet.LastCol; col++)
            if (Math.Abs(ws.Column(col).Width - template.ColumnWidths[col - 1]) > 0.01)
                problems.Add($"cột {col}: Mẫu {ws.Column(col).Width} ≠ bản gốc {template.ColumnWidths[col - 1]}");

        ShouldHaveNoProblems(problems);
    }

    [Theory]
    [MemberData(nameof(SheetNames))]
    public void Row_heights_match(string name)
    {
        var sheet = Sheets[name];
        var template = sheet.Create();
        var ws = Mau.Value.Worksheet(name);
        var problems = new List<string>();
        var regionRows = template.Repeat is null
            ? new HashSet<int>()
            : Enumerable.Range(RegionFirstRow, RegionRows).ToHashSet();

        for (var row = 1; row <= sheet.LastRow; row++)
        {
            if (regionRows.Contains(row)) continue;
            var expected = template.RowHeights.TryGetValue(row, out var h) ? h : 15.0;
            if (Math.Abs(ws.Row(row).Height - expected) > 0.01)
                problems.Add($"hàng {row}: Mẫu {ws.Row(row).Height} ≠ bản gốc {expected}");
        }

        if (template.Repeat is { } region)
        {
            Height(region.ProjectRow.Height, RegionProjectRow);
            Height(region.ViTriRow.Height, RegionViTriRow);
            Height(region.DotRow.Height, RegionDotRow);
            Height(region.BlankRow.Height, RegionBlankRow);
        }

        ShouldHaveNoProblems(problems);

        void Height(double expected, int mauRow)
        {
            if (Math.Abs(ws.Row(mauRow).Height - expected) > 0.01)
                problems.Add($"hàng mẫu vùng lặp (Mẫu hàng {mauRow}): Mẫu {ws.Row(mauRow).Height} ≠ bản gốc {expected}");
        }
    }

    // ===== ô gộp =====

    [Theory]
    [MemberData(nameof(SheetNames))]
    public void Merged_ranges_match(string name)
    {
        var sheet = Sheets[name];
        var template = sheet.Create();
        var ws = Mau.Value.Worksheet(name);

        var fromMau = ws.MergedRanges
            .Where(range => range.RangeAddress.FirstAddress.ColumnNumber <= sheet.LastCol)
            .Select(range => range.RangeAddress.ToStringRelative())
            .ToHashSet();
        var fromTemplate = template.Cells
            .Where(cell => cell.RowSpan > 1 || cell.ColSpan > 1)
            .Select(cell => $"{XLHelper.GetColumnLetterFromNumber(cell.Col)}{cell.Row}:" +
                            $"{XLHelper.GetColumnLetterFromNumber(cell.Col + cell.ColSpan - 1)}{cell.Row + cell.RowSpan - 1}")
            .ToHashSet();
        var extras = ExtraMerges.GetValueOrDefault(name, Array.Empty<string>());

        var onlyInTemplate = fromTemplate.Except(fromMau).Except(extras).ToList();
        var onlyInMau = fromMau.Except(fromTemplate).ToList();
        var missingExtras = extras.Except(fromTemplate).ToList();

        var problems = new List<string>();
        problems.AddRange(onlyInTemplate.Select(range => $"gộp {range} chỉ có ở bản gốc"));
        problems.AddRange(onlyInMau.Select(range => $"gộp {range} chỉ có ở Mẫu"));
        problems.AddRange(missingExtras.Select(range => $"gộp thêm {range} đã khai nhưng bản gốc không có"));
        ShouldHaveNoProblems(problems);
    }

    // ===== từng ô của bản gốc đối chiếu ô Mẫu =====

    [Theory]
    [MemberData(nameof(SheetNames))]
    public void Every_template_cell_matches_the_Mau_cell(string name)
    {
        var template = Sheets[name].Create();
        var ws = Mau.Value.Worksheet(name);
        var problems = new List<string>();

        foreach (var cell in template.Cells)
            CompareCell(name, ws, cell, cell.Row, problems);

        if (template.Repeat is { } region)
        {
            CompareRegionRow(name, ws, region.ProjectRow, RegionProjectRow, problems);
            CompareRegionRow(name, ws, region.ViTriRow, RegionViTriRow, problems);
            CompareRegionRow(name, ws, region.DotRow, RegionDotRow, problems);
            CompareRegionRow(name, ws, region.BlankRow, RegionBlankRow, problems);
        }

        ShouldHaveNoProblems(problems);
    }

    private static void CompareRegionRow(string name, IXLWorksheet ws, TemplateRow row, int mauRow, List<string> problems)
    {
        foreach (var cell in row.Cells)
            CompareCell(name, ws, cell with { Row = mauRow }, mauRow, problems, skipText: true);
    }

    private static void CompareCell(
        string sheet, IXLWorksheet ws, TemplateCell cell, int mauRow, List<string> problems, bool skipText = false)
    {
        var where = Where(sheet, mauRow, cell.Col);
        var mau = ws.Cell(mauRow, cell.Col);
        var style = cell.Style;

        // --- kiểu ---
        var hasText = cell.Segments.Count > 0 || !string.IsNullOrWhiteSpace(mau.GetString());
        if (hasText && mau.Style.Font.FontName != "Times New Roman")
            problems.Add($"{where}: phông Mẫu {mau.Style.Font.FontName}");
        if (Math.Abs(mau.Style.Font.FontSize - style.FontSize) > 0.01)
            problems.Add($"{where}: cỡ chữ Mẫu {mau.Style.Font.FontSize} ≠ {style.FontSize}");
        if (mau.Style.Font.Bold != style.Bold) problems.Add($"{where}: đậm Mẫu {mau.Style.Font.Bold} ≠ {style.Bold}");
        if (mau.Style.Font.Italic != style.Italic) problems.Add($"{where}: nghiêng Mẫu {mau.Style.Font.Italic} ≠ {style.Italic}");
        var underlined = mau.Style.Font.Underline != XLFontUnderlineValues.None;
        if (underlined != style.Underline) problems.Add($"{where}: gạch chân Mẫu {underlined} ≠ {style.Underline}");
        if (ToAlign(mau.Style.Alignment.Horizontal) != style.Align)
            problems.Add($"{where}: canh ngang Mẫu {mau.Style.Alignment.Horizontal} ≠ {style.Align}");
        if (ToVAlign(mau.Style.Alignment.Vertical) != style.VAlign)
            problems.Add($"{where}: canh dọc Mẫu {mau.Style.Alignment.Vertical} ≠ {style.VAlign}");
        if (mau.Style.Alignment.WrapText != style.Wrap) problems.Add($"{where}: xuống dòng Mẫu {mau.Style.Alignment.WrapText} ≠ {style.Wrap}");
        if (style.NumberFormat is not null && MauNumberFormat(mau) != style.NumberFormat)
            problems.Add($"{where}: định dạng số Mẫu «{MauNumberFormat(mau)}» ≠ «{style.NumberFormat}»");

        var borders = MauBorders(ws, mauRow, cell.Col, cell.RowSpan, cell.ColSpan);
        if (borders != style.Borders) problems.Add($"{where}: viền Mẫu {borders} ≠ bản gốc {style.Borders}");

        if (skipText) return;

        // --- chữ ---
        var mauText = Nfc(mau.GetString());
        var key = $"{sheet}!{XLHelper.GetColumnLetterFromNumber(cell.Col)}{mauRow}";
        var kinds = cell.Segments.Select(s => s.Kind).ToList();
        var year = (Func<string, string>)(text => text.Replace("{năm}", "2026"));

        if (kinds.Any(k => k is SegmentKind.Computed or SegmentKind.Overridable))
        {
            // Ô tự tính: Mẫu phải để công thức, hoặc để trống (app tự tính cả những chỗ Mẫu quên).
            var acceptable = mau.HasFormula || string.IsNullOrWhiteSpace(mauText) || LiteralTextWhereMauHasBug.Contains(key)
                             || kinds.Contains(SegmentKind.Overridable);
            if (!acceptable) problems.Add($"{where}: ô tự tính nhưng Mẫu có chữ «{mauText}»");
            return;
        }

        if (kinds.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(mauText)) problems.Add($"{where}: Mẫu có chữ «{mauText}» nhưng bản gốc không có đoạn nào");
            return;
        }

        // Ô số tự nhập: Mẫu có thể để sẵn một số ví dụ (KS chi C19 = 0); số ví dụ không phải nội dung biểu mẫu.
        if (kinds.All(k => k == SegmentKind.InputNumber)) return;

        if (kinds.All(k => k == SegmentKind.Fixed))
        {
            var fixedText = year(string.Concat(cell.Segments.Select(s => s.Text)));
            if (Nfc(fixedText) != mauText) problems.Add($"{where}: chữ Mẫu «{mauText}» ≠ bản gốc «{fixedText}»");
            return;
        }

        // Ô có chỗ điền: chữ Mẫu phải khớp một trong ba cách ghép (chấm chấm / ghi chú / điền sẵn),
        // so sau khi gộp khoảng trắng.
        string Join(Func<TemplateSegment, string> pick) => Squash(year(string.Concat(cell.Segments.Select(pick))));
        var candidates = new[]
        {
            Join(s => s.Text),
            Join(s => s.Kind == SegmentKind.Fixed ? s.Text : s.Hint ?? ""),
            Join(s => s.Kind == SegmentKind.Fixed ? s.Text : s.Default ?? s.Text),
        };
        if (!candidates.Contains(Squash(mauText)))
            problems.Add($"{where}: chữ Mẫu «{mauText}» không khớp bản gốc ({string.Join(" | ", candidates.Distinct())})");
    }

    /// ClosedXML trả `Format` rỗng cho định dạng có sẵn của Excel (id 3 = `#,##0`, id 37 = `#,##0_);(#,##0)`).
    private static string MauNumberFormat(IXLCell cell)
    {
        var format = cell.Style.NumberFormat;
        if (format.Format.Length > 0) return format.Format;
        return format.NumberFormatId switch
        {
            3 => "#,##0",
            37 => "#,##0_);(#,##0)",
            var id => $"(định dạng có sẵn số {id})",
        };
    }

    private static CellBorders MauBorders(IXLWorksheet ws, int row, int col, int rowSpan, int colSpan)
    {
        BorderLine Of(XLBorderStyleValues value) => value switch
        {
            XLBorderStyleValues.None => BorderLine.None,
            XLBorderStyleValues.Hair => BorderLine.Hair,
            _ => BorderLine.Thin,
        };

        BorderLine Edge(IEnumerable<IXLCell> cells, Func<IXLCell, XLBorderStyleValues> pick)
        {
            var lines = cells.Select(c => Of(pick(c))).Where(line => line != BorderLine.None).ToList();
            return lines.Count == 0 ? BorderLine.None : lines.All(l => l == BorderLine.Hair) ? BorderLine.Hair : BorderLine.Thin;
        }

        IEnumerable<IXLCell> Column(int c) => Enumerable.Range(row, rowSpan).Select(r => ws.Cell(r, c));
        IEnumerable<IXLCell> RowCells(int r) => Enumerable.Range(col, colSpan).Select(c => ws.Cell(r, c));

        return new CellBorders(
            Left: Edge(Column(col), c => c.Style.Border.LeftBorder),
            Right: Edge(Column(col + colSpan - 1), c => c.Style.Border.RightBorder),
            Top: Edge(RowCells(row), c => c.Style.Border.TopBorder),
            Bottom: Edge(RowCells(row + rowSpan - 1), c => c.Style.Border.BottomBorder));
    }

    private static CellAlign ToAlign(XLAlignmentHorizontalValues value) => value switch
    {
        XLAlignmentHorizontalValues.General => CellAlign.General,
        XLAlignmentHorizontalValues.Left => CellAlign.Left,
        XLAlignmentHorizontalValues.Center => CellAlign.Center,
        XLAlignmentHorizontalValues.Right => CellAlign.Right,
        XLAlignmentHorizontalValues.Justify => CellAlign.Justify,
        _ => throw new NotSupportedException($"Mẫu dùng canh ngang chưa hỗ trợ: {value}")
    };

    private static CellVAlign ToVAlign(XLAlignmentVerticalValues value) => value switch
    {
        XLAlignmentVerticalValues.Bottom => CellVAlign.Bottom,
        XLAlignmentVerticalValues.Center => CellVAlign.Center,
        XLAlignmentVerticalValues.Top => CellVAlign.Top,
        _ => throw new NotSupportedException($"Mẫu dùng canh dọc chưa hỗ trợ: {value}")
    };

    // ===== chiều ngược lại: Mẫu có gì thì bản gốc phải có =====

    [Theory]
    [MemberData(nameof(SheetNames))]
    public void Every_Mau_cell_with_text_or_border_is_covered_by_the_template(string name)
    {
        var sheet = Sheets[name];
        var template = sheet.Create();
        var ws = Mau.Value.Worksheet(name);
        var problems = new List<string>();

        var covered = new HashSet<(int Row, int Col)>();
        foreach (var cell in template.Cells)
            for (var r = cell.Row; r < cell.Row + cell.RowSpan; r++)
            for (var c = cell.Col; c < cell.Col + cell.ColSpan; c++)
                covered.Add((r, c));
        if (template.Repeat is not null)
            for (var r = RegionFirstRow; r < RegionFirstRow + RegionRows; r++)
            for (var c = 1; c <= sheet.LastCol; c++)
                covered.Add((r, c));

        for (var row = 1; row <= sheet.LastRow; row++)
        for (var col = 1; col <= sheet.LastCol; col++)
        {
            var cell = ws.Cell(row, col);
            var hasBorder = MauBorders(ws, row, col, 1, 1).Any;
            var hasText = !string.IsNullOrWhiteSpace(cell.GetString()) || cell.HasFormula;
            if (!hasText && !hasBorder) continue;
            if (MauNotes.Contains(Where(name, row, col))) continue;
            if (!covered.Contains((row, col)))
                problems.Add($"{Where(name, row, col)}: Mẫu có {(hasText ? "chữ/công thức" : "viền")} nhưng bản gốc không phủ ô này");
        }

        ShouldHaveNoProblems(problems);
    }

    // ===== những thứ riêng =====

    [Fact]
    public void Budget_code_defaults_show_the_same_text_as_the_Mau_number_formats()
    {
        var template = ThongTriTemplate.Create();
        var ws = Mau.Value.Worksheet("Thông tri");
        var defaults = template.Repeat!.ProjectRow.Cells
            .Where(cell => cell.Col <= 6)
            .OrderBy(cell => cell.Col)
            .Select(cell => cell.Segments.Single().Default)
            .ToList();

        for (var col = 1; col <= 6; col++)
            defaults[col - 1].ShouldBe(ws.Cell(RegionProjectRow, col).GetFormattedString(), $"cột {col}");
    }

    [Theory]
    [MemberData(nameof(SheetNames))]
    public void Segment_keys_are_unique_per_input_and_never_empty(string name)
    {
        var template = Sheets[name].Create();
        var inputs = template.AllSegments()
            .Where(s => s.Kind is SegmentKind.Input or SegmentKind.InputNumber or SegmentKind.Overridable or SegmentKind.SettingInput)
            .ToList();

        inputs.ShouldAllBe(s => !string.IsNullOrWhiteSpace(s.Key));
        // Khoá "...@line" của ô trong vùng lặp được gắn khoá dòng lúc dựng tờ, nên hai hàng mẫu dùng chung là đúng.
        inputs.Where(s => !s.Key!.EndsWith(FormSheetResolver.LineScope, StringComparison.Ordinal))
            .GroupBy(s => s.Key).Where(g => g.Count() > 1).Select(g => g.Key)
            .ShouldBeEmpty("khoá ô nhập bị trùng — hai ô cùng ghi vào một chỗ");
        template.AllSegments().Where(s => s.Kind is SegmentKind.Computed)
            .ShouldAllBe(s => !string.IsNullOrWhiteSpace(s.Key));
    }
}
