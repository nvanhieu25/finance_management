using ConstructionFinance.Domain.Common;
namespace ConstructionFinance.Domain.Finance;
public class Package : BaseEntity
{
    public int CostGroupId { get; set; }
    public CostGroup CostGroup { get; set; } = null!;
    public string Name { get; set; } = "";
    public string? ContractorName { get; set; }
    public string? BankAccount { get; set; }
    public decimal ContractValue { get; set; }
    public decimal ContractAnnexValue { get; set; }
    public int SortOrder { get; set; }
    public List<Dot> Dots { get; set; } = new();
}
