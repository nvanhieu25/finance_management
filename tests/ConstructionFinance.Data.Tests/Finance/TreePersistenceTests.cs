using ConstructionFinance.Data.Tests;
using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Finance;

public class TreePersistenceTests
{
    [Fact]
    public async Task Save_and_load_full_tree_roundtrips()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var project = new Project
        {
            Name = "DA1",
            ViTris = { new ViTri { Name = "Vị trí 1", SortOrder = 0,
                CostGroups = { new CostGroup { Name = "XÂY DỰNG",
                    Packages = { new Package { Name = "Gói 1", ContractValue = 50_000_000_000m,
                        Dots = { new Dot { Date = new DateOnly(2026,3,1), Fund = Fund.NgoaiHoi, Advance = 10_000_000m } } } } } } } }
        };
        ctx.FinanceProjects.Add(project);
        await ctx.SaveChangesAsync();

        using var ctx2 = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(ctx.Database.GetDbConnection()).Options);
        var loaded = await ctx2.FinanceProjects
            .Include(p => p.ViTris).ThenInclude(v => v.CostGroups)
            .ThenInclude(g => g.Packages).ThenInclude(pk => pk.Dots)
            .SingleAsync();

        loaded.ViTris[0].CostGroups[0].Packages[0].Dots[0].Fund.ShouldBe(Fund.NgoaiHoi);
        loaded.ViTris[0].CostGroups[0].Packages[0].ContractValue.ShouldBe(50_000_000_000m);
    }

    [Fact]
    public async Task Deleting_project_cascades_to_dots()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var project = new Project
        {
            Name = "DA1",
            ViTris = { new ViTri { Name = "V1", CostGroups = { new CostGroup { Name = "G",
                Packages = { new Package { Name = "P", Dots = { new Dot { Date = new DateOnly(2026,3,1), Fund = Fund.NSNN, Advance = 1m } } } } } } } }
        };
        ctx.FinanceProjects.Add(project);
        await ctx.SaveChangesAsync();

        // Delete from a fresh context (sharing the same connection) with only a stub
        // entity attached — no children loaded/tracked — so the deletion can only
        // succeed via SQLite's real ON DELETE CASCADE, not the EF change-tracker's
        // client-side cascade.
        using var deleteCtx = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(ctx.Database.GetDbConnection()).Options);
        deleteCtx.FinanceProjects.Remove(new Project { Id = project.Id, Name = "DA1" });
        await deleteCtx.SaveChangesAsync();

        using var verifyCtx = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(ctx.Database.GetDbConnection()).Options);
        (await verifyCtx.Dots.CountAsync()).ShouldBe(0);
        (await verifyCtx.Packages.CountAsync()).ShouldBe(0);
    }
}
