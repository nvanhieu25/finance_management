using System.Collections.ObjectModel;
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// Read-only "Quyết toán năm" tab: one row per fiscal year (1/2→31/1), split
/// DPNSNN / Quỹ DTNH, + a TỔNG CỘNG row. Live via Refresh() from the Vị trí tabs.
public class YearSettlementViewModel : ViewModelBase
{
    public ObservableCollection<YearSettlementRow> Rows { get; } = new();

    public void Refresh(IReadOnlyList<ViTri> viTris)
    {
        var agg = TreeCalculator.Roll(viTris.Select(TreeCalculator.AggregateViTri).ToList());

        Rows.Clear();
        foreach (var y in agg.Yearly.OrderBy(y => y.FiscalYear))
            Rows.Add(new YearSettlementRow
            {
                YearLabel = $"Quyết toán năm {y.FiscalYear}",
                NSNN = MoneyText.Format(y.NSNN),
                NgoaiHoi = MoneyText.Format(y.NgoaiHoi),
                Total = MoneyText.Format(y.NSNN + y.NgoaiHoi)
            });

        if (Rows.Count == 0) return;   // no đợt → no year rows, no total

        var totalNsnn = agg.Yearly.Sum(y => y.NSNN);
        var totalNgoaiHoi = agg.Yearly.Sum(y => y.NgoaiHoi);
        Rows.Add(new YearSettlementRow
        {
            YearLabel = "TỔNG CỘNG",
            NSNN = MoneyText.Format(totalNsnn),
            NgoaiHoi = MoneyText.Format(totalNgoaiHoi),
            Total = MoneyText.Format(totalNsnn + totalNgoaiHoi),
            IsBold = true
        });
    }
}
