namespace ConstructionFinance.Domain.Entities;

public class AppSetting
{
    public required string Key { get; set; }
    public string? Value { get; set; }
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; }
}
