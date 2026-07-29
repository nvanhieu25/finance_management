using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Finance;

public class TreeEntitiesTests
{
    [Fact]
    public void Tree_navigations_default_to_empty_and_wire_up()
    {
        var project = new Project { Name = "DA1" };
        var viTri = new ViTri { Name = "Vị trí 1" };
        var group = new CostGroup { Name = "CHI PHÍ XÂY DỰNG" };
        var package = new Package { Name = "Gói 1", ContractValue = 50_000_000_000m };
        var dot = new Dot { Date = new DateOnly(2026, 3, 1), Fund = Fund.NgoaiHoi, Advance = 10_000_000m };

        project.ViTris.Add(viTri);
        viTri.CostGroups.Add(group);
        group.Packages.Add(package);
        package.Dots.Add(dot);

        project.ViTris.ShouldHaveSingleItem();
        viTri.CostGroups[0].Packages[0].Dots[0].Fund.ShouldBe(Fund.NgoaiHoi);
        new Package { Name = "x" }.Dots.ShouldBeEmpty();
    }
}
