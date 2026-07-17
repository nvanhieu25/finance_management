namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// The pinned "TH" page — wraps the existing Tổng hợp view-model unchanged.
public sealed class SummaryPageViewModel : WorkbookPageViewModel
{
    public SummaryPageViewModel(FinanceSummaryViewModel summary)
    {
        Summary = summary;
        Title = "TH";
    }

    public FinanceSummaryViewModel Summary { get; }

    public override bool IsPinned => true;

    public override object Content => Summary;
}
