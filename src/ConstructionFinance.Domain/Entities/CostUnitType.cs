namespace ConstructionFinance.Domain.Entities;

public class CostUnitType
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
}
