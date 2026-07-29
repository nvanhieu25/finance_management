namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// One fiscal-year settlement line (or the bold TỔNG CỘNG). String facade.
public class YearSettlementRow
{
    public string YearLabel { get; init; } = "";
    public string NSNN { get; init; } = "";
    public string NgoaiHoi { get; init; } = "";
    public string Total { get; init; } = "";
    public bool IsBold { get; init; }
}
