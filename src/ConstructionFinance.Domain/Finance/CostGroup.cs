using ConstructionFinance.Domain.Common;
namespace ConstructionFinance.Domain.Finance;
public class CostGroup : BaseEntity
{
    public int ViTriId { get; set; }
    public ViTri ViTri { get; set; } = null!;
    public required string Name { get; set; }
    public int SortOrder { get; set; }
    public List<Package> Packages { get; set; } = new();
}
