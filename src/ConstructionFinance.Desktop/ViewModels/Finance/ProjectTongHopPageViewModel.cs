namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// The pinned per-project "Tổng hợp" tab (first in the Vị trí tab strip).
public sealed class ProjectTongHopPageViewModel : WorkbookPageViewModel
{
    public ProjectTongHopPageViewModel(ProjectTongHopViewModel inner)
    {
        Inner = inner;
        Title = "Tổng hợp";
    }

    public ProjectTongHopViewModel Inner { get; }
    public override object Content => Inner;
}
