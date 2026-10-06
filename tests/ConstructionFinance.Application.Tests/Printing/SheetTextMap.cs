using ClosedXML.Excel;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// Đọc chữ theo (dòng, cột) từ cả hai đầu ra của tính năng In, để so chúng với nhau.
/// Cả hai bản đồ dùng toạ độ 1-based như ClosedXML, nên so trực tiếp được.
internal static class SheetTextMap
{
    /// Chuẩn hoá trước khi so. Ô tiền trong .xlsx là SỐ THẬT mang định dạng "#,##0", nên chữ
    /// đọc ra phụ thuộc văn hoá của máy chạy test ("50,000,000,000" hay "50.000.000.000"),
    /// còn MigraDoc nhận chuỗi đã dựng theo quy ước Việt. Bỏ dấu phân nghìn để so theo CHỮ SỐ.
    ///
    /// Nói thẳng cái mất đi: sau khi chuẩn hoá, test này không còn phân biệt được dấu chấm với
    /// dấu phẩy. Quy ước dấu chấm do PrintMoneyTextTests ghim riêng.
    public static string Normalize(string raw) =>
        raw.Replace(".", "").Replace(",", "").Replace(" ", " ").Trim();

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
        return string.Concat(cell.Elements.OfType<Paragraph>()
            .Select(paragraph => string.Concat(paragraph.Elements.OfType<Text>().Select(text => text.Content))));
    }
}
