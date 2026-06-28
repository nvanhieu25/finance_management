using System.Collections.Generic;
using System.Linq;

namespace ConstructionFinance.Application.Calc;

/// Single source of the Excel formulas. Pure; no DB/UI dependency.
public static class ProjectCalculator
{
    /// A đợt is an advance only when it fills Tạm ứng and no TT KLHT; otherwise a payment (default).
    public static LineItemKind InferKind(LineItemInput x) =>
        x.Advance > 0m && x.PaymentKLHT == 0m ? LineItemKind.Advance : LineItemKind.Payment;

    public static LineItemComputed ComputeLine(LineItemInput x)
    {
        var kind = InferKind(x);
        var disbursed = x.DisbursedNSNN + x.DisbursedNgoaiHoi;
        var expected = kind == LineItemKind.Advance
            ? x.Advance
            : x.PaymentKLHT - x.RecoveredAdvance;
        return new LineItemComputed(kind, disbursed, expected, disbursed == expected);
    }

    public static ProjectAggregate Aggregate(IReadOnlyList<LineItemInput> items)
    {
        var sumAdvance = items.Sum(i => i.Advance);
        var sumRecovered = items.Sum(i => i.RecoveredAdvance);
        var sumKlht = items.Sum(i => i.PaymentKLHT);
        var sumNsnn = items.Sum(i => i.DisbursedNSNN);
        var sumNgoaiHoi = items.Sum(i => i.DisbursedNgoaiHoi);

        return new ProjectAggregate(
            SumAdvance: sumAdvance,
            SumRecovered: sumRecovered,
            SumKLHT: sumKlht,
            SumNSNN: sumNsnn,
            SumNgoaiHoi: sumNgoaiHoi,
            UnrecoveredAdvance: sumAdvance - sumRecovered,
            TotalDisbursed: sumNsnn + sumNgoaiHoi,
            YearlySettlements: BuildYearly(items));
    }

    private static IReadOnlyList<YearSettlement> BuildYearly(IReadOnlyList<LineItemInput> items) =>
        items
            .GroupBy(i => i.Date.Year)
            .OrderBy(g => g.Key)
            .Select(g => new YearSettlement(
                Year: g.Key,
                NSNN: g.Sum(i => i.DisbursedNSNN),
                NgoaiHoi: g.Sum(i => i.DisbursedNgoaiHoi),
                KLHT: g.Sum(i => i.PaymentKLHT),
                Advance: g.Sum(i => i.Advance)))
            .ToList();
}
