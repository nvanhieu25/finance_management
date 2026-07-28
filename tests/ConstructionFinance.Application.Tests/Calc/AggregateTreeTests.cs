using ConstructionFinance.Application.Calc;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Calc;

public class AggregateTreeTests
{
    [Fact]
    public void AggregateTree_rolls_whole_project()
    {
        var project = new Project
        {
            Name = "DA1",
            ViTris =
            {
                new ViTri
                {
                    Name = "Vị trí 1",
                    CostGroups =
                    {
                        new CostGroup
                        {
                            Name = "XÂY DỰNG",
                            Packages =
                            {
                                new Package
                                {
                                    Name = "Gói 1",
                                    Dots =
                                    {
                                        new Dot { Date = new DateOnly(2026,3,1), Fund = Fund.NSNN, Advance = 10_000_000m },
                                        new Dot { Date = new DateOnly(2026,6,1), Fund = Fund.NSNN, PaymentKLHT = 200_000_000m, RecoveredAdvance = 5_000_000m },
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var a = TreeCalculator.AggregateTree(project);

        a.DisbursedNSNN.ShouldBe(205_000_000m);
        a.TotalDisbursed.ShouldBe(205_000_000m);
        a.UnrecoveredAdvance.ShouldBe(5_000_000m);
        a.Yearly.ShouldHaveSingleItem();
        a.Yearly[0].FiscalYear.ShouldBe(2026);
    }
}
