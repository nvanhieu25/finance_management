// FinanceProjectRepositoryTreeTests.cs
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Data.Tests;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Finance;

public class FinanceProjectRepositoryTreeTests
{
    [Fact]
    public async Task GetTreeAsync_loads_all_four_levels()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);
        var id = await repo.AddAsync(new Project
        {
            Name = "DA1",
            ViTris = { new ViTri { Name = "V1", CostGroups = { new CostGroup { Name = "G",
                Packages = { new Package { Name = "P",
                    Dots = { new Dot { Date = new DateOnly(2026,3,1), Fund = Fund.NSNN, Advance = 7m } } } } } } } }
        });

        var tree = await repo.GetTreeAsync(id);

        tree.ShouldNotBeNull();
        tree!.ViTris[0].CostGroups[0].Packages[0].Dots[0].Advance.ShouldBe(7m);
    }
}
