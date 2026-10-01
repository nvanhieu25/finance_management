using System.Text;

namespace ConstructionFinance.Application.Printing;

/// Tên sheet Excel: tối đa 31 ký tự, không được chứa \ / * ? : [ ] và không được
/// bắt đầu hoặc kết thúc bằng dấu nháy đơn. Tách ra từ FinanceExportService (7E sẽ xóa
/// tệp đó) để cả đường xuất cũ lẫn tính năng in dùng chung một quy tắc. Hành vi giữ
/// nguyên so với hàm gốc, trừ một chỗ cố ý sửa: cắt không xé đôi cặp surrogate (xem
/// TextTruncation) — hàm gốc cắt cứng, nên tên có emoji vắt qua ranh giới làm ClosedXML ném.
public static class ExcelSheetName
{
    private const int MaxLength = 31;

    /// Tên khi làm sạch xong không còn gì (rỗng, toàn khoảng trắng, toàn ký tự cấm, hoặc
    /// null). "DA" là giá trị hàm gốc đang dùng; giữ nguyên để việc chuyển chỗ không đổi
    /// tên sheet của bản xuất cũ. Với tính năng in đây chỉ là lưới an toàn.
    private const string Fallback = "DA";

    /// Bảy ký tự Excel cấm hẳn, cộng dấu nháy đơn. Excel chỉ cấm nháy đơn ở hai đầu tên,
    /// nhưng ta loại nó ở MỌI vị trí: nếu chỉ tỉa hai đầu rồi mới cắt còn 31 ký tự, một
    /// dấu nháy nằm giữa tên có thể trở thành dấu cuối sau khi cắt, và Excel vẫn từ chối.
    private static readonly char[] Illegal = { '\\', '/', '*', '?', ':', '[', ']', '\'' };

    /// Làm sạch `raw`, cắt còn 31 ký tự, và nếu đã có trong `used` thì thêm hậu tố
    /// " (2)", " (3)"… (cắt bớt phần tên để cả tên lẫn hậu tố vẫn vừa 31 ký tự). Tên trả
    /// về được ghi vào `used`. Excel coi tên sheet là KHÔNG phân biệt hoa thường ("Abc"
    /// và "abc" trùng nhau, ClosedXML ném ArgumentException), nên `used` phải dựng bằng
    /// StringComparer.OrdinalIgnoreCase.
    public static string Unique(string? raw, HashSet<string> used)
    {
        var cleaned = Clean(raw);
        if (used.Add(cleaned)) return cleaned;

        for (var n = 2; ; n++)
        {
            var suffix = $" ({n})";
            var candidate = TextTruncation.Truncate(cleaned, MaxLength - suffix.Length) + suffix;
            if (used.Add(candidate)) return candidate;
        }
    }

    private static string Clean(string? raw)
    {
        var sb = new StringBuilder();
        foreach (var ch in raw ?? "")
            if (Array.IndexOf(Illegal, ch) < 0)
                sb.Append(ch);

        var text = sb.ToString().Trim();
        if (text.Length == 0) return Fallback;
        return TextTruncation.Truncate(text, MaxLength);
    }
}
