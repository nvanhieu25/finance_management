namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// The per-project "Quyết toán năm" tab (last in the Vị trí tab strip).
public sealed class YearSettlementPageViewModel : WorkbookPageViewModel
{
    public YearSettlementPageViewModel(YearSettlementViewModel inner)
    {
        Inner = inner;
        Title = "Quyết toán năm";
    }

    public YearSettlementViewModel Inner { get; }
    public override object Content => Inner;
}
