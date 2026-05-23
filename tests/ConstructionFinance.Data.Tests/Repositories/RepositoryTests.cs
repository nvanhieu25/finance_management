using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Repositories;

public class RepositoryTests
{
    [Fact]
    public async Task Add_then_GetById_returns_entity()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        IRepository<Vendor> repo = new Repository<Vendor>(ctx);

        await repo.AddAsync(new Vendor { VendorName = "X" });
        await ctx.SaveChangesAsync();

        var all = await repo.ListAsync();
        var first = all.Single();
        var fetched = await repo.GetByIdAsync(first.Id);
        fetched.ShouldNotBeNull();
        fetched!.VendorName.ShouldBe("X");
    }

    [Fact]
    public async Task SoftDelete_marks_DeletedAt_and_hides_from_default_query()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        IRepository<Vendor> repo = new Repository<Vendor>(ctx);
        await repo.AddAsync(new Vendor { VendorName = "X" });
        await ctx.SaveChangesAsync();
        var v = (await repo.ListAsync()).Single();

        await repo.SoftDeleteAsync(v);
        await ctx.SaveChangesAsync();

        (await repo.ListAsync()).ShouldBeEmpty();
        (await repo.ListIncludingDeletedAsync()).Single().DeletedAt.ShouldNotBeNull();
    }
}
