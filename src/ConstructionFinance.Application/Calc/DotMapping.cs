using ConstructionFinance.Domain.Finance;
namespace ConstructionFinance.Application.Calc;

public static class DotMapping
{
    public static DotInput ToDotInput(this Dot d) =>
        new(d.Date, d.Fund, d.Advance, d.PaymentKLHT, d.RecoveredAdvance);
}
