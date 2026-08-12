using System.Text;

namespace ConstructionFinance.Application.Forms;

/// Đọc số tiền đồng thành chữ theo quy ước văn bản nhà nước:
/// "nghìn" (không phải "ngàn"), "linh" (không phải "lẻ"), và sau "mươi" thì
/// 1 → "mốt", 4 → "tư", 5 → "lăm". Thay cho add-in [3]!VND() của Mẫu.xlsx
/// (add-in đó không có giấy phép tự do nên không dùng được).
public static class VietnameseNumberToWords
{
    private static readonly string[] Digits =
        { "không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };

    // Hậu tố cho từng nhóm 3 chữ số, tính từ nhóm thấp nhất lên.
    // Nhóm thứ 4 (10^12) lại đọc là "nghìn tỷ", nhóm thứ 5 là "triệu tỷ".
    private static readonly string[] GroupSuffixes = { "", "nghìn", "triệu", "tỷ", "nghìn tỷ", "triệu tỷ" };

    public static string ToWords(decimal amount)
    {
        var rounded = Math.Round(amount, 0, MidpointRounding.AwayFromZero);
        if (rounded == 0m) return "Không đồng.";

        var negative = rounded < 0m;
        var value = Math.Abs(rounded);

        var groups = SplitIntoGroups(value);
        var parts = new List<string>();
        for (var i = groups.Count - 1; i >= 0; i--)
        {
            if (groups[i] == 0) continue;

            // "không trăm" chỉ cần khi nhóm này đứng SAU một nhóm đã đọc.
            var isLeading = parts.Count == 0;
            var text = ReadGroup(groups[i], forceHundreds: !isLeading);
            var suffix = GroupSuffixes[i];
            parts.Add(suffix.Length == 0 ? text : $"{text} {suffix}");
        }

        var sentence = string.Join(" ", parts);
        if (negative) sentence = "âm " + sentence;

        return char.ToUpperInvariant(sentence[0]) + sentence[1..] + " đồng.";
    }

    /// Tách số thành các nhóm 3 chữ số, chỉ số 0 là nhóm hàng đơn vị.
    private static List<int> SplitIntoGroups(decimal value)
    {
        var groups = new List<int>();
        while (value > 0m)
        {
            groups.Add((int)(value % 1000m));
            value = Math.Floor(value / 1000m);
        }
        return groups;
    }

    /// Đọc một nhóm 3 chữ số. forceHundreds=true thì hàng trăm bằng 0 vẫn đọc
    /// "không trăm" (vì nhóm này đứng sau một nhóm lớn hơn đã được đọc).
    private static string ReadGroup(int group, bool forceHundreds)
    {
        var hundreds = group / 100;
        var tens = group / 10 % 10;
        var units = group % 10;

        var sb = new StringBuilder();

        if (hundreds > 0 || forceHundreds)
            sb.Append(Digits[hundreds]).Append(" trăm");

        if (tens == 0)
        {
            if (units > 0)
            {
                if (sb.Length > 0) sb.Append(" linh");
                sb.Append(sb.Length > 0 ? " " : "").Append(Digits[units]);
            }
            return sb.ToString();
        }

        if (sb.Length > 0) sb.Append(' ');
        sb.Append(tens == 1 ? "mười" : $"{Digits[tens]} mươi");

        if (units == 0) return sb.ToString();

        sb.Append(' ').Append(units switch
        {
            1 when tens > 1 => "mốt",
            4 when tens > 1 => "tư",
            5 => "lăm",
            _ => Digits[units]
        });

        return sb.ToString();
    }
}
