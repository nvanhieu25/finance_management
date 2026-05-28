using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Data.Seed;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class CostUnitTypeServiceTests
{
    [Fact]
    public async Task GetAllAsync_returns_seeded_units_sorted_by_code()
    {
        using var h = new ServiceTestHarness();
        CostUnitTypeSeeder.Seed(h.Context);
        ICostUnitTypeService svc = new CostUnitTypeService(new CostUnitTypeRepository(h.Context));

        var list = await svc.GetAllAsync();

        list.Count.ShouldBeGreaterThanOrEqualTo(10);
        list.ShouldContain(u => u.Code == "m2");
        list.ShouldContain(u => u.Code == "m3");
        list[0].Code.CompareTo(list[^1].Code).ShouldBeLessThanOrEqualTo(0); // sorted ascending
    }

    [Fact]
    public async Task GetAllAsync_on_empty_database_returns_empty_list()
    {
        using var h = new ServiceTestHarness();
        ICostUnitTypeService svc = new CostUnitTypeService(new CostUnitTypeRepository(h.Context));

        var list = await svc.GetAllAsync();

        list.ShouldBeEmpty();
    }
}
