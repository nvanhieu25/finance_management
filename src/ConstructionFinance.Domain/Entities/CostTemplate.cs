namespace ConstructionFinance.Domain.Entities;

public class CostTemplate
{
    public int Id { get; set; }
    public required string TemplateName { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<CostTemplateItem> Items { get; set; } = new List<CostTemplateItem>();
}
