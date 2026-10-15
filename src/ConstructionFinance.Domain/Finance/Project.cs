using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Finance;

/// Dự án của sổ tài chính theo cây Dự án → Vị trí → Chi phí → Gói thầu → Đợt. Nằm ở namespace riêng
/// để không trùng Entities.Project (nhóm CRUD tiền-6A, còn dùng ở mục Quyết định).
public class Project : BaseEntity
{
    public string? Code { get; set; }
    public required string Name { get; set; }
    public string? Notes { get; set; }

    public List<ViTri> ViTris { get; set; } = new();

    /// Chữ người dùng tự gõ trên ba biểu mẫu của dự án này (finance_form_inputs).
    public List<FormInput> FormInputs { get; set; } = new();
}
