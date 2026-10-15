using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Finance;

/// Project-centric flat model (new). Lives in its own namespace during the
/// strangler transition so it does not clash with Entities.Project (legacy).
public class Project : BaseEntity
{
    public string? Code { get; set; }
    public required string Name { get; set; }
    public string? ContractorName { get; set; }
    public decimal ContractValue { get; set; }
    public decimal ContractAnnexValue { get; set; }
    public string? Notes { get; set; }

    public List<ProjectLineItem> Lines { get; set; } = new();
    public List<ProjectDecision> Decisions { get; set; } = new();
    public List<ViTri> ViTris { get; set; } = new();

    /// Chữ người dùng tự gõ trên ba biểu mẫu của dự án này (finance_form_inputs).
    public List<FormInput> FormInputs { get; set; } = new();

    // Chuyển tiếp tới Task 11: ba bản khai dưới đây không còn ánh xạ vào DB (xem FinanceProjectConfiguration).
    public ThongTriDeclaration? ThongTri { get; set; }
    public KsChiDeclaration? KsChi { get; set; }
    public GdnttDeclaration? Gdntt { get; set; }
}
