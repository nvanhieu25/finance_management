namespace ConstructionFinance.Application.Calc;

/// Single source of the Excel formulas. Pure; no DB/UI dependency.
public static class ProjectCalculator
{
    /// A đợt is an advance only when it fills Tạm ứng and no TT KLHT; otherwise a payment (default).
    public static LineItemKind InferKind(LineItemInput input) =>
        input.Advance > 0m && input.PaymentKLHT == 0m ? LineItemKind.Advance : LineItemKind.Payment;

    public static LineItemComputed ComputeLine(LineItemInput input)
    {
        var kind = InferKind(input);
        var disbursed = input.DisbursedNSNN + input.DisbursedNgoaiHoi;
        var expected = kind == LineItemKind.Advance
            ? input.Advance
            : input.PaymentKLHT - input.RecoveredAdvance;
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

    /// Fold many project aggregates into a grand total (the "Tổng cộng" row).
    /// Field-wise sum; yearly settlements merged by year.
    public static ProjectAggregate Combine(IReadOnlyList<ProjectAggregate> aggregates)
    {
        var yearly = aggregates
            .SelectMany(a => a.YearlySettlements)
            .GroupBy(y => y.Year)
            .OrderBy(g => g.Key)
            .Select(g => new YearSettlement(
                Year: g.Key,
                NSNN: g.Sum(y => y.NSNN),
                NgoaiHoi: g.Sum(y => y.NgoaiHoi),
                KLHT: g.Sum(y => y.KLHT),
                Advance: g.Sum(y => y.Advance)))
            .ToList();

        return new ProjectAggregate(
            SumAdvance: aggregates.Sum(a => a.SumAdvance),
            SumRecovered: aggregates.Sum(a => a.SumRecovered),
            SumKLHT: aggregates.Sum(a => a.SumKLHT),
            SumNSNN: aggregates.Sum(a => a.SumNSNN),
            SumNgoaiHoi: aggregates.Sum(a => a.SumNgoaiHoi),
            UnrecoveredAdvance: aggregates.Sum(a => a.UnrecoveredAdvance),
            TotalDisbursed: aggregates.Sum(a => a.TotalDisbursed),
            YearlySettlements: yearly);
    }
}
