using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Finance;

public class FinanceProjectRepositoryTests
{
    private static Project TreeProject(string name) => new()
    {
        Name = name,
        ViTris =
        {
            new ViTri { Name = "Vị trí 1", CostGroups =
            {
                new CostGroup { Name = "CHI PHÍ XÂY DỰNG", Packages =
                {
                    new Package { Name = "Gói A", ContractValue = 1_000m, ContractAnnexValue = 200m, Dots =
                    {
                        new Dot { Date = new DateOnly(2026, 1, 5), Fund = Fund.NSNN, PaymentKLHT = 800m, RecoveredAdvance = 200m }
                    } }
                } }
            } }
        }
    };

    [Fact]
    public async Task Repository_adds_lists_loads_the_tree_and_deletes()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);

        var id = await repo.AddAsync(TreeProject("DA1"));
        ctx.ChangeTracker.Clear();

        (await repo.ListAsync()).Count.ShouldBe(1);
        var tree = await repo.GetTreeAsync(id);
        var package = tree!.ViTris.Single().CostGroups.Single().Packages.Single();
        package.ContractAnnexValue.ShouldBe(200m);          // phụ lục HĐ lưu và đọc lại đúng
        package.Dots.Single().PaymentKLHT.ShouldBe(800m);

        await repo.DeleteAsync(id);
        ctx.ChangeTracker.Clear();
        (await repo.ListAsync()).ShouldBeEmpty();
        ctx.Dots.Count().ShouldBe(0);                       // xoá dự án xoá cả cây (cascade)
    }

    [Fact]
    public async Task ListTreesAsync_returns_every_project_with_its_tree_ordered_by_name()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);
        await repo.AddAsync(TreeProject("B"));
        await repo.AddAsync(TreeProject("A"));
        ctx.ChangeTracker.Clear();

        var trees = await repo.ListTreesAsync();

        trees.Select(t => t.Name).ShouldBe(new[] { "A", "B" });
        trees.ShouldAllBe(t => t.ViTris.Single().CostGroups.Single().Packages.Single().Dots.Count == 1);
    }
}
