using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Application.Forms;

/// Hiện một số theo định dạng ô của Mẫu cho màn hình và bản in (đường .xlsx đặt thẳng chuỗi định
/// dạng cho Excel). Chỉ hỗ trợ đúng các định dạng Mẫu dùng: `#,##0`, `#,##0_);(#,##0)`,
/// `#,##0"đ"`, `#,##0\ "đ"`. Dấu phân nghìn là dấu chấm (quy ước Việt, như PrintMoneyText).
public static class FormNumberFormat
{
    public const string Default = "#,##0";

    public static string Format(decimal value, string? format)
    {
        format ??= Default;
        var suffix = format.Contains("\\ \"đ\"") ? " đ" : format.Contains("\"đ\"") ? "đ" : "";
        var digits = Group(Math.Abs(value));
        if (value >= 0) return digits + suffix;
        return format.Contains(");(") ? $"({digits}){suffix}" : $"-{digits}{suffix}";
    }

    public static string Group(decimal value) => PrintMoneyText.Format(value);
}
