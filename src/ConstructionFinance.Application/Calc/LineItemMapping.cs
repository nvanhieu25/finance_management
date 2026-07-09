using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Calc;

public static class LineItemMapping
{
    public static LineItemInput ToCalcInput(this ProjectLineItem x) =>
        new(x.Date, x.PackageName, x.Advance, x.PaymentKLHT, x.RecoveredAdvance, x.DisbursedNSNN, x.DisbursedNgoaiHoi);
}
