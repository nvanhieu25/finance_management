namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// One fiscal-year settlement line (or the bold TỔNG CỘNG). String facade for the grid, plus the typed
/// amounts the print needs.
public class YearSettlementRow
{
    public string YearLabel { get; init; } = "";
    public string NSNN { get; init; } = "";
    public string NgoaiHoi { get; init; } = "";
    public string Total { get; init; } = "";
    public bool IsBold { get; init; }

    /// SỐ thật của hai cột tiền NSNN / NgoaiHoi ở trên (cột Total là tổng của hai số này). Bản in ghi
    /// các số này để cộng được trong Excel thay vì đọc ngược chuỗi. Null với dòng dựng tay.
    public decimal? NsnnAmount { get; init; }
    public decimal? NgoaiHoiAmount { get; init; }
}
