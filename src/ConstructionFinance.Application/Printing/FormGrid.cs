using ConstructionFinance.Application.Forms;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace ConstructionFinance.Application.Printing;

/// Lưới ô thưa, toạ độ 1-based, dùng để chép bố cục ba biểu mẫu từ ClosedXML sang MigraDoc
/// gần như từng dòng một: Text/Money/Bold/Center/Merge/Indent/Border đứng song song với
/// ws.Cell(...).Value / Style.Font.Bold / Range(...).Merge() của bộ vẽ .xlsx. Người review
/// đối chiếu được từng dòng giữa hai bộ vẽ — đó là lý do lớp này tồn tại thay vì dựng Table
/// trực tiếp.
public sealed class FormGrid
{
    private const double GridBorderPoints = 0.5;

    private readonly IReadOnlyList<double> _columnWidthChars;
    private readonly Dictionary<(int Row, int Column), string> _text = new();
    private readonly HashSet<(int Row, int Column)> _bold = new();
    private readonly HashSet<(int Row, int Column)> _center = new();
    private readonly HashSet<(int Row, int Column)> _right = new();
    private readonly HashSet<(int Row, int Column)> _bordered = new();
    private readonly Dictionary<(int Row, int Column), int> _indent = new();
    private readonly Dictionary<(int Row, int Column), (int Rows, int Columns)> _merges = new();
    private int _rowCount;

    public FormGrid(IReadOnlyList<double> columnWidthChars)
    {
        if (columnWidthChars.Count == 0)
            throw new ArgumentException("Lưới biểu mẫu phải có ít nhất một cột.", nameof(columnWidthChars));
        _columnWidthChars = columnWidthChars;
    }

    public FormGrid Text(int row, int column, string? value)
    {
        Touch(row, column);
        _text[(row, column)] = value ?? "";
        _right.Remove((row, column));   // ghi chữ đè lên ô tiền thì hết canh phải
        return this;
    }

    /// Excel canh phải ô số theo mặc định nên bộ vẽ .xlsx (SetMoney) không bao giờ nói ra;
    /// MigraDoc thì canh trái, nên bản chép trung thành phải nói rõ ở đây.
    public FormGrid Money(int row, int column, decimal value)
    {
        Text(row, column, PrintMoneyText.Format(value));
        _right.Add((row, column));
        return this;
    }

    public FormGrid Bold(int fromRow, int fromColumn, int toRow, int toColumn) =>
        Mark(_bold, fromRow, fromColumn, toRow, toColumn);

    public FormGrid Center(int fromRow, int fromColumn, int toRow, int toColumn) =>
        Mark(_center, fromRow, fromColumn, toRow, toColumn);

    public FormGrid Border(int fromRow, int fromColumn, int toRow, int toColumn) =>
        Mark(_bordered, fromRow, fromColumn, toRow, toColumn);

    public FormGrid Indent(int row, int column, int level)
    {
        Touch(row, column);
        _indent[(row, column)] = level;
        return this;
    }

    /// Như ws.Range(fromRow, fromColumn, toRow, toColumn).Merge(). Gộp tường minh THẮNG luật
    /// tự gộp ở Render.
    public FormGrid Merge(int fromRow, int fromColumn, int toRow, int toColumn)
    {
        Touch(toRow, toColumn);
        Touch(fromRow, fromColumn);
        _merges[(fromRow, fromColumn)] = (toRow - fromRow, toColumn - fromColumn);
        return this;
    }

    public Table Render(Section section, PrintOrientation orientation)
    {
        var scale = ExcelFitScale.ScaleFor(_columnWidthChars, orientation);

        var table = section.AddTable();
        table.Format.Font.Size = Unit.FromPoint(FormExcelExporter.FontSize * scale);

        foreach (var width in _columnWidthChars)
            table.AddColumn(Unit.FromMillimeter(ExcelFitScale.ContentWidthMm(new[] { width }) * scale));

        for (var row = 0; row < _rowCount; row++) table.AddRow();

        for (var row = 1; row <= _rowCount; row++)
        for (var column = 1; column <= _columnWidthChars.Count; column++)
            FillCell(table, row, column, scale);

        return table;
    }

    private void FillCell(Table table, int row, int column, double scale)
    {
        var cell = table.Rows[row - 1]!.Cells[column - 1]!;

        if (_bold.Contains((row, column))) cell.Format.Font.Bold = true;
        if (_bordered.Contains((row, column))) cell.Borders.Width = Unit.FromPoint(GridBorderPoints);

        if (_merges.TryGetValue((row, column), out var span))
        {
            if (span.Rows > 0) cell.MergeDown = span.Rows;
            if (span.Columns > 0) cell.MergeRight = span.Columns;
        }

        if (!_text.TryGetValue((row, column), out var text) || text.Length == 0) return;

        // Luật bù tràn chữ: Excel cho chữ tràn sang ô TRỐNG bên phải, MigraDoc thì xuống dòng
        // trong lòng ô. Ô nào có chữ dài hơn cột của nó mà chưa gộp tường minh thì tự gộp sang
        // phải, dừng trước ô đã có chữ — đúng chỗ Excel cắt.
        if (!_merges.ContainsKey((row, column)) && text.Length > _columnWidthChars[column - 1])
        {
            var extra = 0;
            for (var next = column + 1; next <= _columnWidthChars.Count; next++)
            {
                // Chữ tràn của Excel đi xuyên qua ô trông trống: ô ghi "" không phải bức tường.
                // Bộ vẽ .xlsx ghi `?? ""` vào ô ở rất nhiều chỗ (form.DonViCapTren ?? "" ...), nên
                // nếu "" chặn thì kết quả thay đổi theo dữ liệu so với Excel.
                // Cũng dừng ở ô thuộc một lệnh gộp khác (gốc gộp, hoặc ô bị MergeRight/MergeDown của
                // ô khác phủ lên), kẻo sinh ra hai vùng gộp chồng nhau.
                if (IsInMerge(row, next)) break;
                if (_text.TryGetValue((row, next), out var neighbour) && !string.IsNullOrWhiteSpace(neighbour)) break;
                extra++;
            }
            if (extra > 0) cell.MergeRight = extra;
        }

        var paragraph = cell.AddParagraph(text);
        if (_center.Contains((row, column))) paragraph.Format.Alignment = ParagraphAlignment.Center;
        if (_right.Contains((row, column))) paragraph.Format.Alignment = ParagraphAlignment.Right;
        if (_indent.TryGetValue((row, column), out var level) && level > 0)
            // LeftIndent, KHÔNG phải FirstLineIndent: Alignment.Indent của Excel dịch MỌI dòng của
            // ô, kể cả dòng đã xuống dòng; FirstLineIndent chỉ dịch dòng đầu (như MigraDocBuilder.WriteRow).
            paragraph.Format.LeftIndent =
                Unit.FromMillimeter(ExcelFitScale.ContentWidthMm(new[] { 3.0 * level }) * scale);
    }

    private bool IsInMerge(int row, int column)
    {
        foreach (var (anchor, span) in _merges)
            if (row >= anchor.Row && row <= anchor.Row + span.Rows &&
                column >= anchor.Column && column <= anchor.Column + span.Columns)
                return true;
        return false;
    }

    private FormGrid Mark(HashSet<(int Row, int Column)> target, int fromRow, int fromColumn, int toRow, int toColumn)
    {
        Touch(fromRow, fromColumn);
        Touch(toRow, toColumn);
        for (var row = fromRow; row <= toRow; row++)
        for (var column = fromColumn; column <= toColumn; column++)
            target.Add((row, column));
        return this;
    }

    /// Ghi vào dòng xa thì các dòng ở giữa tự sinh ra, như ClosedXML. Cột thì không: số cột là
    /// cố định của biểu mẫu, ghi ra ngoài là lỗi của bên gọi và phải nổ ngay.
    private void Touch(int row, int column)
    {
        if (row < 1) throw new ArgumentOutOfRangeException(nameof(row), row, "Dòng tính từ 1.");
        if (column < 1 || column > _columnWidthChars.Count)
            throw new ArgumentOutOfRangeException(nameof(column), column,
                $"Lưới chỉ có {_columnWidthChars.Count} cột.");
        if (row > _rowCount) _rowCount = row;
    }
}
