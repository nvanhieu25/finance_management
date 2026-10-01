namespace ConstructionFinance.Application.Printing;

/// Cắt chuỗi theo đơn vị UTF-16 mà không xé đôi cặp surrogate. Một surrogate cao lẻ ở cuối
/// không phải XML hợp lệ: ClosedXML ném ArgumentException khi lưu workbook, làm hỏng cả lần in.
internal static class TextTruncation
{
    /// Trả về `text` nguyên vẹn nếu đã vừa `maxLength`; nếu không thì cắt còn tối đa `maxLength`
    /// đơn vị UTF-16, lùi thêm một ký tự khi dao rơi giữa một cặp surrogate (nên kết quả có thể
    /// ngắn hơn `maxLength` một đơn vị). `maxLength` phải từ 1 trở lên.
    public static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;

        var end = char.IsHighSurrogate(text[maxLength - 1]) ? maxLength - 1 : maxLength;
        return text[..end];
    }
}
