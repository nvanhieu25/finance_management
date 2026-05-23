using ConstructionFinance.Data.Seed;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Seed;

public class CostUnitTypeSeederTests
{
    [Fact]
    public void Seed_inserts_default_units_when_empty()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        ctx.CostUnitTypes.Count().ShouldBe(0);

        CostUnitTypeSeeder.Seed(ctx);

        var codes = ctx.CostUnitTypes.Select(u => u.Code).ToList();
        codes.ShouldContain("m2");
        codes.ShouldContain("m3");
        codes.ShouldContain("kg");
        codes.ShouldContain("pcs");
        codes.Count.ShouldBeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public void Seed_is_idempotent()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        CostUnitTypeSeeder.Seed(ctx);
        var firstCount = ctx.CostUnitTypes.Count();

        CostUnitTypeSeeder.Seed(ctx);

        ctx.CostUnitTypes.Count().ShouldBe(firstCount);
    }
}
