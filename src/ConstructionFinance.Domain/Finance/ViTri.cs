using ConstructionFinance.Domain.Common;
namespace ConstructionFinance.Domain.Finance;
public class ViTri : BaseEntity
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public required string Name { get; set; }
    public string? NguonVon { get; set; }
    public int SortOrder { get; set; }
    public List<CostGroup> CostGroups { get; set; } = new();
}
