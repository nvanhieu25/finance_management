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
}
