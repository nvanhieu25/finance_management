using System.Globalization;
using System.Text;

namespace ConstructionFinance.Desktop.Common;

/// Tên tệp gợi ý cho bản in. Bỏ dấu tiếng Việt và loại ký tự Windows cấm để tên không hỏng
/// khi mang sang máy đích. Dự án và tên Vị trí do người dùng gõ nên có thể chứa bất cứ thứ gì.
public static class PrintFileName
{
    /// Chín ký tự Windows cấm trong tên tệp. Viết cứng thay vì Path.GetInvalidFileNameChars():
    /// hàm đó trả theo hệ điều hành đang chạy (trên Linux chỉ có '/' và '\0'), còn tệp in phải
    /// mở được trên Windows dù app được chạy ở đâu.
    private static readonly char[] WindowsForbidden = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

    /// `<prefix>-<sheetName>-<yyyyMMdd-HHmm>.xlsx`. Phần nào rỗng sau khi làm sạch thì bỏ luôn
    /// cả dấu gạch nối nó. Hàm thuần: với bất kỳ chuỗi nào cũng trả về một tên, không ném.
    public static string Suggest(string? prefix, string sheetName, DateTime now)
    {
        // Invariant: yyyy theo lịch Gregorian dù máy đặt vùng nào (th-TH sẽ ra năm Phật lịch).
        var stamp = now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        var parts = new[] { Clean(prefix), Clean(sheetName), stamp }.Where(part => part.Length > 0);
        return string.Join("-", parts) + ".xlsx";
    }

    private static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        // string.Normalize ném ArgumentException khi gặp surrogate đứt đôi, nên bỏ chúng TRƯỚC khi
        // chuẩn hóa. EnumerateRunes trả U+FFFD cho mỗi đoạn không hợp lệ; U+FFFD thật trong tên
        // cũng bị bỏ theo, vô hại vì nó chẳng có nghĩa gì trong một tên tệp.
        var valid = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
            if (rune != Rune.ReplacementChar)
                valid.Append(rune);

        var decomposed = valid.ToString().Normalize(NormalizationForm.FormD);

        var cleaned = new StringBuilder(decomposed.Length);
        var pendingSpace = false;
        foreach (var ch in decomposed)
        {
            // FormD tách "ợ" thành "o" + các dấu rời (NonSpacingMark); bỏ dấu rời là xong.
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (Array.IndexOf(WindowsForbidden, ch) >= 0) continue;

            // Gộp mọi dãy khoảng trắng thành một dấu cách, cắt hai đầu. Dấu cách chỉ được ghi ra khi
            // chữ kế tiếp đến, nên khoảng trắng ở đầu/cuối tự rơi mất và ký tự cấm nằm giữa hai dấu
            // cách ("a : b") không để lại hai dấu cách liền nhau.
            if (char.IsWhiteSpace(ch))
            {
                pendingSpace = cleaned.Length > 0;
                continue;
            }
            if (pendingSpace)
            {
                cleaned.Append(' ');
                pendingSpace = false;
            }

            // Riêng đ/Đ không có dạng tách (không có dấu rời nào để bỏ) nên phải đổi tay.
            cleaned.Append(ch switch { 'đ' => 'd', 'Đ' => 'D', _ => ch });
        }
        return cleaned.ToString();
    }
}
