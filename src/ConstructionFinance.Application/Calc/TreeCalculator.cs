// TreeCalculator.cs
using System.Linq;
using ConstructionFinance.Domain.Finance;
namespace ConstructionFinance.Application.Calc;

/// Pure calc for the Vị trí tree. No DB/UI. Single source of the Excel formulas.
public static class TreeCalculator
{
    /// Fiscal year runs 1/2/N → 31/1/(N+1): Feb–Dec map to N, Jan maps to N−1.
    public static int FiscalYear(DateOnly d) => d.Month >= 2 ? d.Year : d.Year - 1;

    public static bool IsAdvance(DotInput d) => d.Advance > 0m && d.PaymentKLHT == 0m;

    public static decimal Disbursed(DotInput d) =>
        IsAdvance(d) ? d.Advance : d.PaymentKLHT - d.RecoveredAdvance;

    public static TreeAggregate AggregatePackage(IReadOnlyList<DotInput> dots)
    {
        var nsnn = dots.Where(d => d.Fund == Fund.NSNN).Sum(Disbursed);
        var ngoaiHoi = dots.Where(d => d.Fund == Fund.NgoaiHoi).Sum(Disbursed);
        var sumAdvance = dots.Sum(d => d.Advance);
        var sumRecovered = dots.Sum(d => d.RecoveredAdvance);

        return new TreeAggregate(
            SumAdvance: sumAdvance,
            SumKLHT: dots.Sum(d => d.PaymentKLHT),
            SumRecovered: sumRecovered,
            DisbursedNSNN: nsnn,
            DisbursedNgoaiHoi: ngoaiHoi,
            UnrecoveredAdvance: sumAdvance - sumRecovered,
            TotalDisbursed: nsnn + ngoaiHoi,
            Yearly: BuildYearly(dots));
    }

    private static IReadOnlyList<FiscalYearSettlement> BuildYearly(IReadOnlyList<DotInput> dots) =>
        dots
            .GroupBy(d => FiscalYear(d.Date))
            .OrderBy(g => g.Key)
            .Select(g => new FiscalYearSettlement(
                FiscalYear: g.Key,
                NSNN: g.Where(d => d.Fund == Fund.NSNN).Sum(Disbursed),
                NgoaiHoi: g.Where(d => d.Fund == Fund.NgoaiHoi).Sum(Disbursed)))
            .ToList();
}
