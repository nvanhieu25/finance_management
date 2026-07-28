// TreeCalculator.cs
using ConstructionFinance.Domain.Finance;
namespace ConstructionFinance.Application.Calc;

/// Pure calc for the Vị trí tree. No DB/UI. Single source of the Excel formulas.
public static class TreeCalculator
{
    /// Fiscal year runs 1/2/N → 31/1/(N+1): Feb–Dec map to N, Jan maps to N−1.
    public static int FiscalYear(DateOnly d) => d.Month >= 2 ? d.Year : d.Year - 1;
}
