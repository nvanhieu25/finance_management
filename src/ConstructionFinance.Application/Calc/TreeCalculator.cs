// TreeCalculator.cs
using System.Linq;
using ConstructionFinance.Domain.Finance;
namespace ConstructionFinance.Application.Calc;

/// Pure calc for the Vị trí tree. No DB/UI. Single source of the Excel formulas.
public static class TreeCalculator
{
    /// Fiscal year runs 1/2/N → 31/1/(N+1): Feb–Dec map to N, Jan maps to N−1.
    public static int FiscalYear(DateOnly d) => d.Month >= 2 ? d.Year : d.Year - 1;

    /// Giải ngân của một đợt = Tạm ứng + Thanh toán KLHT − Thu hồi tạm ứng.
    /// Một đợt có thể vừa mang Tạm ứng vừa mang KLHT trong cùng lần ghi sổ (sổ cho phép
    /// nhập cả hai), nên công thức phải cộng dồn cả ba cột thay vì rẽ nhánh theo loại đợt —
    /// rẽ nhánh sẽ âm thầm bỏ mất phần Tạm ứng của đợt hỗn hợp đó. Với đợt thuần (chỉ tạm ứng
    /// hoặc chỉ thanh toán), công thức rút gọn về đúng giá trị cũ. Khớp cột N = H + I − J của
    /// sổ tham chiếu.
    public static decimal Disbursed(DotInput d) => d.Advance + d.PaymentKLHT - d.RecoveredAdvance;

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

    public static TreeAggregate Roll(IReadOnlyList<TreeAggregate> children)
    {
        var yearly = children
            .SelectMany(c => c.Yearly)
            .GroupBy(y => y.FiscalYear)
            .OrderBy(g => g.Key)
            .Select(g => new FiscalYearSettlement(
                FiscalYear: g.Key,
                NSNN: g.Sum(y => y.NSNN),
                NgoaiHoi: g.Sum(y => y.NgoaiHoi)))
            .ToList();

        return new TreeAggregate(
            SumAdvance: children.Sum(c => c.SumAdvance),
            SumKLHT: children.Sum(c => c.SumKLHT),
            SumRecovered: children.Sum(c => c.SumRecovered),
            DisbursedNSNN: children.Sum(c => c.DisbursedNSNN),
            DisbursedNgoaiHoi: children.Sum(c => c.DisbursedNgoaiHoi),
            UnrecoveredAdvance: children.Sum(c => c.UnrecoveredAdvance),
            TotalDisbursed: children.Sum(c => c.TotalDisbursed),
            Yearly: yearly);
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

    public static TreeAggregate AggregatePackageEntity(Package p) =>
        AggregatePackage(p.Dots.OrderBy(d => d.SortOrder).Select(d => d.ToDotInput()).ToList());

    public static TreeAggregate AggregateGroup(CostGroup g) =>
        Roll(g.Packages.OrderBy(p => p.SortOrder).Select(AggregatePackageEntity).ToList());

    public static TreeAggregate AggregateViTri(ViTri v) =>
        Roll(v.CostGroups.OrderBy(g => g.SortOrder).Select(AggregateGroup).ToList());

    public static TreeAggregate AggregateTree(Project project) =>
        Roll(project.ViTris.OrderBy(v => v.SortOrder).Select(AggregateViTri).ToList());
}
