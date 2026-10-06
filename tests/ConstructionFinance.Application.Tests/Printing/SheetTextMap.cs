using ClosedXML.Excel;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// Đọc chữ theo (dòng, cột) từ cả hai đầu ra của tính năng In, để so chúng với nhau.
/// Cả hai bản đồ dùng toạ độ 1-based như ClosedXML, nên so trực tiếp được.
internal static class SheetTextMap
{
    /// Chuẩn hoá trước khi so. Hai việc:
    ///  1. Khoảng trắng (kể cả xuống dòng và NBSP) gộp thành MỘT dấu cách: ô "Nhà thầu / TK" ghép
    ///     hai dòng bằng "\n", và mỗi bên biểu diễn dấu xuống dòng một kiểu.
    ///  2. CHỈ với ô là SỐ (toàn chữ số, dấu phân cách, dấu trừ đầu) mới bỏ "." và ",". Ô tiền trong
    ///     .xlsx là số thật mang định dạng "#,##0" nên chữ đọc ra phụ thuộc văn hoá máy chạy test
    ///     ("50,000,000,000" hay "50.000.000.000"), còn MigraDoc nhận chuỗi dựng theo quy ước Việt.
    ///     Chữ thường GIỮ NGUYÊN dấu câu: dòng điền của KS chi " + Mục ........, tiểu mục ........"
    ///     người dùng viết tay lên bản in, nên số chấm của nó phải so được.
    ///
    /// Cái mất đi: với ô số, test này không phân biệt được dấu chấm với dấu phẩy. Quy ước dấu chấm do
    /// PrintMoneyTextTests ghim riêng.
    ///
    /// Harness này CHỈ so chữ theo (dòng, cột). Nó KHÔNG so: khoảng gộp ô (MergeRight/MergeDown),
    /// định dạng (đậm, nền, viền, căn lề), bề rộng cột, số dòng thừa ở MigraDoc, tên và thứ tự sheet,
    /// chân trang, cấu hình trang. Những thứ đó thuộc phần render và phải xem bằng mắt.
    public static string Normalize(string raw)
    {
        var text = Regex.Replace(raw, @"\s+", " ").Trim();
        return Regex.IsMatch(text, @"^-?(?=.*\d)[\d.,]+$") ? text.Replace(".", "").Replace(",", "") : text;
    }

    public static IReadOnlyDictionary<(int Row, int Column), string> FromExcel(IXLWorksheet worksheet)
    {
        var map = new Dictionary<(int, int), string>();
        foreach (var cell in worksheet.CellsUsed())
        {
            var text = Normalize(cell.GetFormattedString());
            if (text.Length == 0) continue;
            map[(cell.Address.RowNumber, cell.Address.ColumnNumber)] = text;
        }
        return map;
    }

    public static IReadOnlyDictionary<(int Row, int Column), string> FromMigraDoc(Table table)
    {
        var map = new Dictionary<(int, int), string>();
        for (var row = 0; row < table.Rows.Count; row++)
        for (var column = 0; column < table.Columns.Count; column++)
        {
            var text = Normalize(CellText(table, row, column));
            if (text.Length == 0) continue;
            map[(row + 1, column + 1)] = text;
        }
        return map;
    }

    /// Khẳng định: mọi ô có chữ trong .xlsx đều có ĐÚNG chữ đó, ở ĐÚNG dòng–cột, trong bảng
    /// MigraDoc. Một chiều, có chủ ý — bản MigraDoc được phép có thêm chữ (ví dụ ô tiêu đề mà
    /// Excel để bộ vẽ tự ghi), nhưng không được THIẾU hay LỆCH chỗ.
    public static void AssertSameText(string sheetName, IXLWorksheet worksheet, Table table)
    {
        var excel = FromExcel(worksheet);
        var migraDoc = FromMigraDoc(table);

        var mismatches = new List<string>();
        foreach (var ((row, column), text) in excel.OrderBy(entry => entry.Key))
        {
            if (!migraDoc.TryGetValue((row, column), out var found))
                mismatches.Add($"  ({row},{column}) .xlsx = \"{text}\" — MigraDoc KHÔNG CÓ ô này");
            else if (found != text)
                mismatches.Add($"  ({row},{column}) .xlsx = \"{text}\" — MigraDoc = \"{found}\"");
        }

        if (mismatches.Count == 0) return;

        // KHÔNG dùng ShouldBeEmpty: Shouldly tự kết xuất tập hợp và CẮT NGẮN từng phần tử bằng
        // "...", nên hai ô chữ dài khác nhau ở cuối sẽ hiện ra giống hệt nhau. Danh sách lệch này
        // là chẩn đoán duy nhất của ba task chép biểu mẫu, nên nó phải nguyên vẹn: tự dựng chuỗi
        // rồi Assert.Fail.
        Assert.Fail(
            $"Sheet \"{sheetName}\": hai bộ dựng đã trôi khỏi nhau ở {mismatches.Count} ô:\n"
            + string.Join("\n", mismatches));
    }

    private static string CellText(Table table, int row, int column)
    {
        var cell = table.Rows[row]!.Cells[column]!;
        return string.Concat(cell.Elements.OfType<Paragraph>().Select(paragraph => InlineText(paragraph.Elements)));
    }

    /// MigraDoc đổi "\n" trong AddParagraph/AddText thành Character(LineBreak), nên phải đọc cả
    /// Character, FormattedText (đệ quy) chứ không chỉ Text.
    private static string InlineText(DocumentObjectCollection elements) =>
        string.Concat(elements.Cast<DocumentObject>().Select(element => element switch
        {
            Text text => text.Content,
            FormattedText formatted => InlineText(formatted.Elements),
            Character { SymbolName: SymbolName.LineBreak } => "\n",
            Character { SymbolName: SymbolName.Tab } => "\t",
            Character => " ",
            _ => string.Empty,
        }));
}
