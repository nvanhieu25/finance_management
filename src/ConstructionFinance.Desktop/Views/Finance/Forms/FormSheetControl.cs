using System.ComponentModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;

namespace ConstructionFinance.Desktop.Views.Finance.Forms;

/// Vẽ một ResolvedSheet thành lưới y hệt tờ Excel: cùng hàng, cột, ô gộp, bề rộng, chiều cao, cỡ chữ, đường kẻ.
/// Ô tự nhập là TextBox; ô tự tính là chữ trên nền xám nhạt; ô tự tính cho gõ đè là TextBox có dấu nhận biết
/// khi đã gõ đè và nút ↺ "Trở về tự động".
///
/// Hình học dùng cùng công thức với ExcelFitScale: bề rộng cột = w×7+5 px; hàng pt → px nhân 96/72.
///
/// Cấu trúc (hàng, cột, ô, loại đoạn) KHÔNG đổi giữa hai lần dựng thì chỉ cập nhật chữ tại chỗ — dựng lại cả lưới
/// mỗi lần gõ một phím sẽ làm mất con trỏ. Cấu trúc đổi (Thông tri thêm dòng đợt) thì dựng lại.
public sealed class FormSheetControl : Border
{
    internal const double PixelsPerChar = 7.0;
    internal const double CellPaddingPixels = 5.0;
    internal const double PixelsPerPoint = 96.0 / 72.0;

    private static readonly IBrush Ink = Brushes.Black;
    private static readonly IBrush ComputedFill = new SolidColorBrush(Color.Parse("#F2F2F2"));
    private static readonly IBrush OverriddenFill = new SolidColorBrush(Color.Parse("#FFF1D6"));
    private static readonly IBrush InputFill = new SolidColorBrush(Color.Parse("#EEF4FF"));
    private static readonly IBrush OverrideMarker = new SolidColorBrush(Color.Parse("#E08A00"));
    private static readonly IBrush PaperEdge = new SolidColorBrush(Color.Parse("#B0B0B0"));
    private static readonly FontFamily Serif = new("Times New Roman, Liberation Serif, Tinos, Serif");

    // Canvas chứ không Grid: Grid chia phần dư của ô gộp vào các cột pixel (đo được +2…+4px ở vài cột), làm tờ lệch Mẫu.
    // Mỗi ô đặt tại toạ độ tuyệt đối tính từ bề rộng/chiều cao Mẫu nên hình học không thể trôi.
    private readonly Canvas _canvas = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private double[] _columnX = Array.Empty<double>();
    private double[] _rowY = Array.Empty<double>();
    private readonly Dictionary<string, TextBox> _inputs = new();
    private readonly Dictionary<string, Button> _resets = new();
    private readonly Dictionary<(int Row, int Col), TextBlock> _texts = new();
    private readonly Dictionary<(int Row, int Col), Border> _fills = new();

    private FormPageViewModel? _vm;
    private string? _signature;
    private bool _updating;

    public FormSheetControl()
    {
        Background = Brushes.White;
        BorderBrush = PaperEdge;
        BorderThickness = new Thickness(1);
        Padding = new Thickness(24);
        // Bề rộng cột lẻ (8,43×7+5 = 64,01px) phải giữ đúng: làm tròn từng cột lên sẽ đẩy cả tờ lệch khỏi Mẫu vài px.
        UseLayoutRounding = false;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Child = _canvas;
    }

    public void Attach(FormPageViewModel? vm)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = vm;
        if (vm is not null) vm.PropertyChanged += OnVmPropertyChanged;
        _signature = null;
        Render();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FormPageViewModel.Sheet)) Render();
    }

    private void Render()
    {
        var sheet = _vm?.Sheet;
        if (sheet is null)
        {
            _canvas.Children.Clear();
            _signature = null;
            return;
        }

        var signature = SignatureOf(sheet);
        if (signature == _signature) Update(sheet);
        else
        {
            Build(sheet);
            _signature = signature;
        }
    }

    // ===== dựng =====

    private static string SignatureOf(ResolvedSheet sheet)
    {
        var text = new StringBuilder();
        foreach (var width in sheet.ColumnWidths) text.Append(width).Append(',');
        text.Append('|');
        foreach (var height in sheet.RowHeights) text.Append(height).Append(',');
        foreach (var cell in sheet.Cells)
        {
            text.Append('|').Append(cell.Row).Append(',').Append(cell.Col).Append(',')
                .Append(cell.RowSpan).Append(',').Append(cell.ColSpan).Append(',').Append(cell.TextColSpan);
            foreach (var segment in cell.Segments) text.Append(';').Append((int)segment.Kind).Append(segment.Key);
        }
        return text.ToString();
    }

    private void Build(ResolvedSheet sheet)
    {
        _canvas.Children.Clear();
        _inputs.Clear();
        _resets.Clear();
        _texts.Clear();
        _fills.Clear();

        _columnX = new double[sheet.ColumnWidths.Count + 1];
        for (var i = 0; i < sheet.ColumnWidths.Count; i++)
            _columnX[i + 1] = _columnX[i] + sheet.ColumnWidths[i] * PixelsPerChar + CellPaddingPixels;
        _rowY = new double[sheet.RowHeights.Count + 1];
        for (var i = 0; i < sheet.RowHeights.Count; i++)
            _rowY[i + 1] = _rowY[i] + sheet.RowHeights[i] * PixelsPerPoint;
        _canvas.Width = _columnX[^1];
        _canvas.Height = _rowY[^1];

        // Mỗi ô kẻ cạnh trái/trên của chính nó; cạnh phải/dưới chỉ kẻ khi ô bên cạnh KHÔNG kẻ cạnh trái/trên
        // trùng chỗ — nếu không, hai ô liền nhau cùng kẻ làm đường dày gấp đôi so với Excel.
        var leftEdges = new HashSet<(int Row, int Col)>();
        var topEdges = new HashSet<(int Row, int Col)>();
        foreach (var cell in sheet.Cells)
        {
            if (cell.Style.Borders.Left != BorderLine.None)
                for (var row = cell.Row; row < cell.Row + cell.RowSpan; row++) leftEdges.Add((row, cell.Col));
            if (cell.Style.Borders.Top != BorderLine.None)
                for (var col = cell.Col; col < cell.Col + cell.ColSpan; col++) topEdges.Add((cell.Row, col));
        }

        foreach (var cell in sheet.Cells) AddFillAndBorders(cell, leftEdges, topEdges);
        foreach (var cell in sheet.Cells) AddContent(cell);   // thứ tự hàng-rồi-cột: Tab đi theo chiều đọc
    }

    private void AddFillAndBorders(ResolvedCell cell, HashSet<(int, int)> leftEdges, HashSet<(int, int)> topEdges)
    {
        var borders = cell.Style.Borders;
        var rightShared = Enumerable.Range(cell.Row, cell.RowSpan).All(row => leftEdges.Contains((row, cell.Col + cell.ColSpan)));
        var bottomShared = Enumerable.Range(cell.Col, cell.ColSpan).All(col => topEdges.Contains((cell.Row + cell.RowSpan, col)));

        var frame = new Border
        {
            BorderBrush = Ink,
            BorderThickness = new Thickness(
                Thickness(borders.Left),
                Thickness(borders.Top),
                rightShared ? 0 : Thickness(borders.Right),
                bottomShared ? 0 : Thickness(borders.Bottom)),
            IsHitTestVisible = false,
        };

        // Ô chữ tự tính tràn sang ô bên cạnh ("Bằng chữ…") không tô xám: nền xám chỉ phủ ô đầu sẽ cắt ngang chữ.
        if (cell.IsComputed && cell.TextColSpan <= cell.ColSpan)
        {
            frame.Background = cell.Segments.Any(s => s.IsOverridden) ? OverriddenFill : ComputedFill;
            _fills[(cell.Row, cell.Col)] = frame;
        }

        Place(frame, cell.Row, cell.Col, cell.RowSpan, cell.ColSpan);
    }

    private static double Thickness(BorderLine line) => line switch
    {
        BorderLine.Thin => 1.0,
        BorderLine.Hair => 0.8,
        _ => 0.0,
    };

    private void AddContent(ResolvedCell cell)
    {
        if (cell.Segments.Count == 0) return;

        Control content;
        if (!cell.IsEditable)
        {
            var block = NewText(cell, cell.DisplayText);
            block.HorizontalAlignment = HorizontalAlignment.Stretch;
            block.TextAlignment = TextAlignmentOf(cell);
            _texts[(cell.Row, cell.Col)] = block;
            content = block;
        }
        else
        {
            content = BuildStrip(cell);
        }

        content.VerticalAlignment = cell.Style.VAlign switch
        {
            CellVAlign.Top => VerticalAlignment.Top,
            CellVAlign.Center => VerticalAlignment.Center,
            _ => VerticalAlignment.Bottom,
        };

        // Chữ tràn: nội dung chiếm cả dải TextColSpan, còn đường kẻ ở lớp dưới vẫn chỉ trong ColSpan.
        var span = Math.Min(Math.Max(cell.ColSpan, cell.TextColSpan), _columnX.Length - cell.Col);
        content.HorizontalAlignment = content is TextBlock or TextBox ? HorizontalAlignment.Stretch : content.HorizontalAlignment;

        // Khung cố định bằng đúng dải ô: nội dung dài hơn bị cắt như Excel cắt chữ ở biên ô, và không thể đẩy ô khác.
        var host = new Grid { ClipToBounds = true };
        host.Children.Add(content);
        Place(host, cell.Row, cell.Col, cell.RowSpan, span);
    }

    private Control BuildStrip(ResolvedCell cell)
    {
        var segments = cell.Segments;
        var pixels = cell.Style.FontSize * PixelsPerPoint;
        var align = cell.Style.Align;

        // Một đoạn duy nhất và nó là chỗ điền: ô nhập trải kín ô.
        if (segments.Count == 1)
            return segments[0].Kind == SegmentKind.Overridable
                ? BuildOverridable(cell, segments[0], pixels)
                : BuildInput(cell, segments[0], pixels, fill: true);

        var centered = align is CellAlign.Center or CellAlign.Right;
        // Ô có chữ tràn sang ô bên cạnh (nhãn + chỗ điền ngắn như " + Mục …, tiểu mục …"): chỗ điền cuối chỉ rộng bằng
        // chữ chấm chấm, không trải hết dải tràn — nếu không, ô nhập phủ lên cả các cột số bên dưới.
        var overflowing = cell.TextColSpan > cell.ColSpan;
        var lastIsInput = segments[^1].Kind != SegmentKind.Fixed && !overflowing;

        if (centered)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = align == CellAlign.Center ? HorizontalAlignment.Center : HorizontalAlignment.Right,
            };
            foreach (var segment in segments) panel.Children.Add(StripPart(cell, segment, pixels, fill: false));
            return panel;
        }

        // Canh trái: nhãn đứng yên, chỗ điền cuối cùng chiếm phần còn lại của ô (đúng Excel — chữ nối đuôi nhãn).
        var dock = new DockPanel { LastChildFill = lastIsInput };
        for (var i = 0; i < segments.Count; i++)
        {
            var isLast = i == segments.Count - 1;
            var part = StripPart(cell, segments[i], pixels, fill: lastIsInput && isLast);
            DockPanel.SetDock(part, Dock.Left);
            dock.Children.Add(part);
        }
        return dock;
    }

    private Control StripPart(ResolvedCell cell, ResolvedSegment segment, double pixels, bool fill) =>
        segment.Kind switch
        {
            SegmentKind.Fixed or SegmentKind.Computed => NewText(cell, segment.Text),
            SegmentKind.Overridable => BuildOverridable(cell, segment, pixels),
            _ => BuildInput(cell, segment, pixels, fill),
        };

    private TextBlock NewText(ResolvedCell cell, string text) => new()
    {
        Text = text,
        FontFamily = Serif,
        FontSize = cell.Style.FontSize * PixelsPerPoint,
        FontWeight = cell.Style.Bold ? FontWeight.Bold : FontWeight.Normal,
        FontStyle = cell.Style.Italic ? FontStyle.Italic : FontStyle.Normal,
        TextDecorations = cell.Style.Underline ? TextDecorations.Underline : null,
        Foreground = Ink,
        TextWrapping = cell.Style.Wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(2, 0),
        IsHitTestVisible = false,
    };

    private TextBox BuildInput(ResolvedCell cell, ResolvedSegment segment, double pixels, bool fill)
    {
        var box = new TextBox
        {
            Tag = segment.Key,
            Text = segment.Value,
            Watermark = segment.Hint,
            FontFamily = Serif,
            FontSize = pixels,
            FontWeight = cell.Style.Bold ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = cell.Style.Italic ? FontStyle.Italic : FontStyle.Normal,
            Foreground = Ink,
            Background = InputFill,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(2, 0),
            MinHeight = 0,
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = fill ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
            MinWidth = fill ? 0 : EstimateWidth(segment, pixels),
            TextAlignment = segment.Kind == SegmentKind.InputNumber ? TextAlignment.Right : InputAlignmentOf(cell),
            AcceptsReturn = false,
        };
        box.Classes.Add("gridCell");
        box.BorderThickness = new Thickness(0);
        box.Background = InputFill;

        var key = segment.Key!;
        box.TextChanged += (_, _) =>
        {
            if (!_updating) _vm?.SetInput(key, box.Text);
        };
        // Rời ô: chữ số được định dạng lại có dấu chấm phân nghìn (lúc đang gõ không đụng tới để khỏi nhảy con trỏ).
        box.LostFocus += (_, _) =>
        {
            if (_vm?.Sheet is { } current) Update(current);
        };

        _inputs[key] = box;
        return box;
    }

    private Control BuildOverridable(ResolvedCell cell, ResolvedSegment segment, double pixels)
    {
        var box = BuildInput(cell, segment, pixels, fill: true);
        box.TextAlignment = segment.Number is not null ? TextAlignment.Right : InputAlignmentOf(cell);

        var reset = new Button
        {
            Tag = segment.Key,
            Content = "↺",
            Padding = new Thickness(4, 0),
            MinHeight = 0,
            MinWidth = 0,
            FontSize = pixels * 0.9,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = segment.IsOverridden,
        };
        ToolTip.SetTip(reset, "Trở về tự động");
        var key = segment.Key!;
        reset.Click += (_, _) => _vm?.ResetOverride(key);
        _resets[key] = reset;

        var dock = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(reset, Dock.Right);
        dock.Children.Add(reset);
        dock.Children.Add(box);
        return dock;
    }

    /// Chấm chấm và dấu … hẹp hơn chữ thường nhiều; ước lượng bề rộng ô nhập theo từng loại ký tự.
    private static double EstimateWidth(ResolvedSegment segment, double pixels)
    {
        var sample = segment.Text.Length >= (segment.Hint?.Length ?? 0) ? segment.Text : segment.Hint ?? "";
        var width = sample.Sum(ch => ch is '.' or '…' or ' ' ? 0.3 : 0.55) * pixels;
        return Math.Max(40, width);
    }

    /// Ô nhập một dòng: canh đều hai bên (Justify) của ô nhiều dòng biến chữ mờ thành những khoảng trắng lớn.
    private static TextAlignment InputAlignmentOf(ResolvedCell cell) =>
        cell.Style.Align == CellAlign.Justify ? TextAlignment.Left : TextAlignmentOf(cell);

    private static TextAlignment TextAlignmentOf(ResolvedCell cell) => cell.Style.Align switch
    {
        CellAlign.Center => TextAlignment.Center,
        CellAlign.Right => TextAlignment.Right,
        CellAlign.Justify => TextAlignment.Justify,
        CellAlign.Left => TextAlignment.Left,
        _ => cell.Segments.Any(s => s.Number is not null) ? TextAlignment.Right : TextAlignment.Left,
    };

    private void Place(Control element, int row, int col, int rowSpan, int colSpan)
    {
        var lastColumn = Math.Min(col - 1 + colSpan, _columnX.Length - 1);
        var lastRow = Math.Min(row - 1 + rowSpan, _rowY.Length - 1);
        element.Width = _columnX[lastColumn] - _columnX[col - 1];
        element.Height = _rowY[lastRow] - _rowY[row - 1];
        Canvas.SetLeft(element, _columnX[col - 1]);
        Canvas.SetTop(element, _rowY[row - 1]);
        _canvas.Children.Add(element);
    }

    // ===== cập nhật tại chỗ =====

    private void Update(ResolvedSheet sheet)
    {
        _updating = true;
        try
        {
            foreach (var cell in sheet.Cells)
            {
                if (_texts.TryGetValue((cell.Row, cell.Col), out var block) && block.Text != cell.DisplayText)
                    block.Text = cell.DisplayText;
                if (_fills.TryGetValue((cell.Row, cell.Col), out var fill))
                    fill.Background = cell.Segments.Any(s => s.IsOverridden) ? OverriddenFill : ComputedFill;

                foreach (var segment in cell.Segments)
                {
                    if (segment.Key is null) continue;
                    if (_inputs.TryGetValue(segment.Key, out var box))
                    {
                        // Ô đang có con trỏ giữ nguyên chữ người dùng đang gõ; rời ô mới chuẩn hoá.
                        if (!box.IsFocused && box.Text != segment.Value) box.Text = segment.Value;
                        box.Watermark = segment.Hint;
                        box.BorderBrush = OverrideMarker;
                        box.BorderThickness = new Thickness(segment.IsOverridden ? 3 : 0, 0, 0, 0);
                    }
                    if (_resets.TryGetValue(segment.Key, out var reset)) reset.IsVisible = segment.IsOverridden;
                }
            }
        }
        finally { _updating = false; }
    }
}
