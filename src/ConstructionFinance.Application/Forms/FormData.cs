using System.Globalization;

namespace ConstructionFinance.Application.Forms;

public readonly record struct ComputedValue(string? Text, decimal? Number);

/// Giá trị các ô TỰ TÍNH, theo khoá của bản gốc. Do `FormDefinition` điền từ kết quả của builder.
public sealed class FormValues
{
    private readonly Dictionary<string, ComputedValue> _values = new(StringComparer.Ordinal);

    /// Đường lỗi (GĐNTT bị chặn) trả về tờ trống: khoá thiếu thì thành ô rỗng thay vì ném. Đường
    /// bình thường để false, nên quên một khoá là lỗi ném ngay chứ không âm thầm ra ô trống.
    public bool BlankMissing { get; init; }

    public FormValues Text(string key, string? text)
    {
        _values[key] = new ComputedValue(text, null);
        return this;
    }

    public FormValues Number(string key, decimal number)
    {
        _values[key] = new ComputedValue(null, number);
        return this;
    }

    public bool TryGet(string key, out ComputedValue value) => _values.TryGetValue(key, out value);
}

/// Chữ người dùng đã gõ cho MỘT biểu mẫu của MỘT dự án, theo khoá của bản gốc. Khoá vắng ≠ khoá
/// có giá trị rỗng: vắng nghĩa là "chưa gõ" (dùng giá trị điền sẵn); rỗng nghĩa là "đã xoá".
public sealed class FormInputSet
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);

    public FormInputSet() { }

    public FormInputSet(IEnumerable<KeyValuePair<string, string?>> initial)
    {
        foreach (var pair in initial) _values[pair.Key] = pair.Value;
    }

    public IReadOnlyDictionary<string, string?> Values => _values;

    public bool Has(string key) => _values.ContainsKey(key);

    public string? Get(string key) => _values.TryGetValue(key, out var value) ? value : null;

    public void Set(string key, string? value) => _values[key] = value;

    public bool Remove(string key) => _values.Remove(key);

    public bool TryGetNumber(string key, out decimal number)
    {
        number = 0m;
        return decimal.TryParse(Get(key), NumberStyles.Number, CultureInfo.InvariantCulture, out number);
    }

    public decimal GetNumber(string key) => TryGetNumber(key, out var number) ? number : 0m;

    /// Dạng lưu của một số: bất biến văn hoá, không dấu phân nghìn.
    public static string ToStored(decimal number) => number.ToString(CultureInfo.InvariantCulture);
}

/// Hai khoá Cài đặt mà ô SettingInput đọc/ghi. Cùng chuỗi với CompanyInfoService.
public static class FormOrgKeys
{
    public const string DonViCapTren = "don_vi_cap_tren";
    public const string DonViCoSo = "don_vi_co_so";

    public static string? Get(this FormOrgInfo org, string key) => key switch
    {
        DonViCapTren => org.DonViCapTren,
        DonViCoSo => org.DonViCoSo,
        _ => throw new ArgumentException($"Khoá Cài đặt không biết: \"{key}\".", nameof(key))
    };

    public static FormOrgInfo With(this FormOrgInfo org, string key, string? value) => key switch
    {
        DonViCapTren => org with { DonViCapTren = value },
        DonViCoSo => org with { DonViCoSo = value },
        _ => throw new ArgumentException($"Khoá Cài đặt không biết: \"{key}\".", nameof(key))
    };
}
