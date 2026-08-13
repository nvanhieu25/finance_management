namespace ConstructionFinance.Desktop.Common;

/// Tên dự án do người dùng đặt nên có thể chứa ký tự Windows không cho phép trong
/// tên tệp. Dùng chung cho tên tệp gợi ý của ba biểu mẫu.
public static class FileNames
{
    public static string Sanitize(string? raw)
    {
        var cleaned = new string((raw ?? "").Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)).ToArray())
            .Replace(' ', '_')
            .Trim('_');
        return cleaned.Length == 0 ? "DuAn" : cleaned;
    }
}
