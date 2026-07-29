// FinanceProjectRepositoryReplaceTreeTests.cs
using System.Linq;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Data.Tests;
using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Finance;

public class FinanceProjectRepositoryReplaceTreeTests
{
    [Fact]
    public async Task ReplaceTreeAsync_replaces_whole_tree_and_drops_orphans()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);

        var id = await repo.AddAsync(new Project
        {
            Name = "DA1",
            ViTris =
            {
                new ViTri { Name = "V1", SortOrder = 0,
                    CostGroups = { new CostGroup { Name = "G1", SortOrder = 0,
                        Packages =
                        {
                            new Package { Name = "P1", SortOrder = 0,
                                Dots = { new Dot { Date = new DateOnly(2026,3,1), Fund = Fund.NSNN, Advance = 1m } } },
                            new Package { Name = "P2", SortOrder = 1,
                                Dots = { new Dot { Date = new DateOnly(2026,3,2), Fund = Fund.NSNN, Advance = 2m } } }
                        } } } },
                new ViTri { Name = "V2", SortOrder = 1,
                    CostGroups = { new CostGroup { Name = "G2", SortOrder = 0,
                        Packages = { new Package { Name = "P3", SortOrder = 0,
                            Dots = { new Dot { Date = new DateOnly(2026,3,3), Fund = Fund.NgoaiHoi, Advance = 3m } } } } } } }
            }
        });

        // Mutated tree: same project Id; rename G1, add a Dot to P1, drop P2 entirely.
        var mutated = new Project
        {
            Id = id,
            Name = "DA1",
            ViTris =
            {
                new ViTri { Name = "V1", SortOrder = 0,
                    CostGroups = { new CostGroup { Name = "G1-renamed", SortOrder = 0,
                        Packages =
                        {
                            new Package { Name = "P1", SortOrder = 0,
                                Dots =
                                {
                                    new Dot { Date = new DateOnly(2026,3,1), Fund = Fund.NSNN, Advance = 1m },
                                    new Dot { Date = new DateOnly(2026,3,10), Fund = Fund.NSNN, Advance = 99m }
                                } }
                        } } } },
                new ViTri { Name = "V2", SortOrder = 1,
                    CostGroups = { new CostGroup { Name = "G2", SortOrder = 0,
                        Packages = { new Package { Name = "P3", SortOrder = 0,
                            Dots = { new Dot { Date = new DateOnly(2026,3,3), Fund = Fund.NgoaiHoi, Advance = 3m } } } } } } }
            }
        };

        await repo.ReplaceTreeAsync(mutated);

        using var verifyCtx = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(ctx.Database.GetDbConnection()).Options);
        var verifyRepo = new FinanceProjectRepository(verifyCtx);
        var reloaded = await verifyRepo.GetTreeAsync(id);

        reloaded.ShouldNotBeNull();
        reloaded!.ViTris.Count.ShouldBe(2);
        var v1 = reloaded.ViTris.Single(v => v.Name == "V1");
        v1.CostGroups.Single().Name.ShouldBe("G1-renamed");
        v1.CostGroups.Single().Packages.Count.ShouldBe(1);
        v1.CostGroups.Single().Packages.Single().Name.ShouldBe("P1");
        v1.CostGroups.Single().Packages.Single().Dots.Count.ShouldBe(2);

        // No orphaned rows left behind from the dropped P2 (and its Dot) or the
        // original ViTris/CostGroups/Packages/Dots that were replaced wholesale.
        (await verifyCtx.Packages.CountAsync()).ShouldBe(2); // P1, P3
        (await verifyCtx.Dots.CountAsync()).ShouldBe(3); // 2 under P1, 1 under P3
        (await verifyCtx.CostGroups.CountAsync()).ShouldBe(2); // G1-renamed, G2
        (await verifyCtx.ViTris.CountAsync()).ShouldBe(2); // V1, V2
    }
}
