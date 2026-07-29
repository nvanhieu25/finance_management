using System.Collections.ObjectModel;
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// Read-only "Tổng hợp" tab for one project: a row per Vị trí + a TỔNG CỘNG row,
/// recomputed live from the in-memory Vị trí tabs via Refresh().
public class ProjectTongHopViewModel : ViewModelBase
{
    public ObservableCollection<SummaryDisplayRow> Rows { get; } = new();

    public void Refresh(IReadOnlyList<ViTri> viTris)
    {
        var (rows, total) = WorkbookSummaryBuilder.BuildViTriRows(viTris);
        Rows.Clear();
        foreach (var r in rows) Rows.Add(SummaryDisplayRow.From(r));
        Rows.Add(SummaryDisplayRow.From(total));
    }
}
