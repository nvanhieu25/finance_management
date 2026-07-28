// TreeModels.cs
using ConstructionFinance.Domain.Finance;
namespace ConstructionFinance.Application.Calc;

public record DotInput(DateOnly Date, Fund Fund, decimal Advance, decimal PaymentKLHT, decimal RecoveredAdvance);

public record FiscalYearSettlement(int FiscalYear, decimal NSNN, decimal NgoaiHoi);

public record TreeAggregate(
    decimal SumAdvance,
    decimal SumKLHT,
    decimal SumRecovered,
    decimal DisbursedNSNN,
    decimal DisbursedNgoaiHoi,
    decimal UnrecoveredAdvance,
    decimal TotalDisbursed,
    IReadOnlyList<FiscalYearSettlement> Yearly);
