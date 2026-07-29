using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class ProjectTongHopViewModelTests
{
    private static ViTri ViTriWithAdvance(string name, int sort, decimal advance) => new()
    {
        Name = name,
        SortOrder = sort,
        CostGroups =
        {
            new CostGroup
            {
                Name = "CHI PHÍ XÂY DỰNG", SortOrder = 0,
                Packages =
                {
                    new Package
                    {
                        Name = "Gói A", ContractorName = "Cty A",
                        ContractValue = 50_000_000_000m, SortOrder = 0,
                        Dots =
                        {
                            new Dot { Date = new DateOnly(2026, 3, 1), Fund = Fund.NSNN,
                                      Advance = advance, SortOrder = 0 }
                        }
                    }
                }
            }
        }
    };

    [Fact]
    public void Refresh_lists_a_row_per_vitri_and_a_bold_total()
    {
        var vm = new ProjectTongHopViewModel();

        vm.Refresh(new[]
        {
            ViTriWithAdvance("Vị trí 1", 0, 10_000_000m),
            ViTriWithAdvance("Vị trí 2", 1, 20_000_000m)
        });

        vm.Rows.Count.ShouldBe(3);                 // 2 vị trí + total
        vm.Rows[0].Label.ShouldBe("Vị trí 1");
        vm.Rows[0].IsBold.ShouldBeFalse();
        vm.Rows[0].NSNN.ShouldBe("10.000.000");    // MoneyText "." grouping
        vm.Rows[2].Label.ShouldBe("TỔNG CỘNG");
        vm.Rows[2].IsBold.ShouldBeTrue();
        vm.Rows[2].TotalDisbursed.ShouldBe("30.000.000");
    }

    [Fact]
    public void Refresh_replaces_previous_rows()
    {
        var vm = new ProjectTongHopViewModel();
        vm.Refresh(new[] { ViTriWithAdvance("Vị trí 1", 0, 10_000_000m) });
        vm.Refresh(new[] { ViTriWithAdvance("Vị trí X", 0, 1_000_000m) });

        vm.Rows.Count.ShouldBe(2);                 // 1 vị trí + total (not appended)
        vm.Rows[0].Label.ShouldBe("Vị trí X");
    }

    [Fact]
    public void Zero_money_renders_blank()
    {
        var vm = new ProjectTongHopViewModel();
        vm.Refresh(new[] { ViTriWithAdvance("Vị trí 1", 0, 0m) });

        vm.Rows[0].Advance.ShouldBe("");           // 0 → blank (Excel-like)
    }
}
