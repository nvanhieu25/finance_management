using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class YearSettlementViewModelTests
{
    private static ViTri ViTriWithDot(DateOnly date, Fund fund, decimal advance) => new()
    {
        Name = "V", SortOrder = 0,
        CostGroups =
        {
            new CostGroup
            {
                Name = "G", SortOrder = 0,
                Packages =
                {
                    new Package
                    {
                        Name = "P", SortOrder = 0,
                        Dots = { new Dot { Date = date, Fund = fund, Advance = advance, SortOrder = 0 } }
                    }
                }
            }
        }
    };

    [Fact]
    public void Refresh_makes_a_row_per_fiscal_year_plus_a_total()
    {
        var vm = new YearSettlementViewModel();

        vm.Refresh(new[]
        {
            ViTriWithDot(new DateOnly(2026, 3, 1), Fund.NSNN, 10_000_000m),      // FY 2026
            ViTriWithDot(new DateOnly(2027, 1, 15), Fund.NgoaiHoi, 4_000_000m)   // Jan → FY 2026
        });

        // Both đợt land in fiscal year 2026 (1/2/2026 → 31/1/2027).
        vm.Rows.Count.ShouldBe(2);                          // FY 2026 + total
        vm.Rows[0].YearLabel.ShouldBe("Quyết toán năm 2026");
        vm.Rows[0].NSNN.ShouldBe("10.000.000");
        vm.Rows[0].NgoaiHoi.ShouldBe("4.000.000");
        vm.Rows[0].Total.ShouldBe("14.000.000");
        vm.Rows[1].YearLabel.ShouldBe("TỔNG CỘNG");
        vm.Rows[1].IsBold.ShouldBeTrue();
        vm.Rows[1].Total.ShouldBe("14.000.000");
    }

    [Fact]
    public void Two_distinct_fiscal_years_get_two_rows()
    {
        var vm = new YearSettlementViewModel();

        vm.Refresh(new[]
        {
            ViTriWithDot(new DateOnly(2026, 3, 1), Fund.NSNN, 10_000_000m),      // FY 2026
            ViTriWithDot(new DateOnly(2027, 3, 1), Fund.NSNN, 5_000_000m)        // FY 2027
        });

        vm.Rows.Count.ShouldBe(3);                          // 2026, 2027, total
        vm.Rows[0].YearLabel.ShouldBe("Quyết toán năm 2026");
        vm.Rows[1].YearLabel.ShouldBe("Quyết toán năm 2027");
        vm.Rows[2].Total.ShouldBe("15.000.000");
    }
}
