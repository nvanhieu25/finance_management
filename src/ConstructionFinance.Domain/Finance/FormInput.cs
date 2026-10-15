using ConstructionFinance.Domain.Common;
namespace ConstructionFinance.Domain.Finance;

/// Một ô người dùng tự gõ trên một biểu mẫu của một dự án: (dự án, biểu mẫu, khoá ô) → chữ. Khoá là tên
/// ổn định trong bản gốc biểu mẫu (vd. "SoThongTri"), KHÔNG phải địa chỉ ô, để dời ô không mất chữ; thêm ô
/// mới vào bản gốc không cần migration. Số lưu dạng số bất biến văn hoá ("1500000.5"). `Value == ""` nghĩa
/// là người dùng đã xoá cố ý (khác với không có dòng nào: chưa gõ).
public class FormInput : BaseEntity
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// "thongtri" | "kschi" | "gdntt" (FormDefinition.Id).
    public string Form { get; set; } = "";

    public string Key { get; set; } = "";

    public string? Value { get; set; }
}
