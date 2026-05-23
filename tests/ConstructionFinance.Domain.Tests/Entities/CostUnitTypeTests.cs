using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Entities;

public class CostUnitTypeTests
{
    [Fact]
    public void CostUnitType_can_be_constructed()
    {
        var unit = new CostUnitType
        {
            Code = "m2",
            Name = "Mét vuông",
            Description = "Đơn vị diện tích"
        };

        unit.Code.ShouldBe("m2");
        unit.Name.ShouldBe("Mét vuông");
    }
}
