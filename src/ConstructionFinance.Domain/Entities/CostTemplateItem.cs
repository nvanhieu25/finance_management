namespace ConstructionFinance.Domain.Entities;

public class CostTemplateItem
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public int? ParentId { get; set; }
    public int? DefaultUnitId { get; set; }
    public string? ItemCode { get; set; }
    public required string ItemName { get; set; }
    public int Level { get; set; }
    public int SortOrder { get; set; }

    public CostTemplate Template { get; set; } = null!;
    public CostTemplateItem? Parent { get; set; }
    public ICollection<CostTemplateItem> Children { get; set; } = new List<CostTemplateItem>();
    public CostUnitType? DefaultUnit { get; set; }
}
