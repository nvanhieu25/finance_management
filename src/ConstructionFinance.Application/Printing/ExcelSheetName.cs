using System.Text;

namespace ConstructionFinance.Application.Printing;

/// Tên sheet Excel: tối đa 31 ký tự, không được chứa \ / * ? : [ ] và không được
/// bắt đầu hoặc kết thúc bằng dấu nháy đơn. Mọi sheet của tính năng in đi qua đây. Cắt tên không
/// xé đôi cặp surrogate (xem TextTruncation): cắt cứng theo đơn vị UTF-16 sẽ để lại surrogate lẻ
/// khi tên có emoji vắt qua ranh giới, và ClosedXML ném lúc LƯU workbook.
public static class ExcelSheetName
{
    private const int MaxLength = 31;

    /// Tên khi làm sạch xong không còn gì (rỗng, toàn khoảng trắng, toàn ký tự cấm, hoặc
    /// null). Tên trung tính vì đây chỉ là lưới an toàn: sheet in luôn có tên thật (Tổng hợp, Vị trí…).
    private const string Fallback = "Sheet";

    /// Bảy ký tự Excel cấm hẳn, cộng dấu nháy đơn. Excel chỉ cấm nháy đơn ở hai đầu tên,
    /// nhưng ta loại nó ở MỌI vị trí: nếu chỉ tỉa hai đầu rồi mới cắt còn 31 ký tự, một
    /// dấu nháy nằm giữa tên có thể trở thành dấu cuối sau khi cắt, và Excel vẫn từ chối.
    private static readonly char[] Illegal = { '\\', '/', '*', '?', ':', '[', ']', '\'' };

    /// Làm sạch `raw`, cắt còn 31 ký tự, và nếu đã có trong `used` thì thêm hậu tố
    /// " (2)", " (3)"… (cắt bớt phần tên để cả tên lẫn hậu tố vẫn vừa 31 ký tự). Tên trả
    /// về được ghi vào `used`. Excel coi tên sheet là KHÔNG phân biệt hoa thường ("Abc"
    /// và "abc" trùng nhau, ClosedXML ném ArgumentException), nên `used` phải dựng bằng
    /// StringComparer.OrdinalIgnoreCase. Điều kiện đó nằm ở comparer chứ không ở kiểu, trình biên dịch không giữ được,
    /// nên hàm TỰ KIỂM và ném ArgumentException nói rõ cách sửa thay vì để triệu chứng nổ muộn ở AddWorksheet.
    public static string Unique(string? raw, HashSet<string> used)
    {
        if (!StringComparer.OrdinalIgnoreCase.Equals(used.Comparer))
            throw new ArgumentException(
                "Tập tên đã dùng phải dựng bằng StringComparer.OrdinalIgnoreCase: Excel coi tên sheet là không phân biệt hoa thường, " +
                "nên với comparer khác \"Abc\" và \"abc\" không bị nhận ra là trùng và ClosedXML sẽ ném khi thêm sheet. " +
                "Dựng tập bằng new HashSet<string>(StringComparer.OrdinalIgnoreCase).",
                nameof(used));

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
