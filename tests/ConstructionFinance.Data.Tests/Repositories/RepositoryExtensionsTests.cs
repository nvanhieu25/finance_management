using ConstructionFinance.Data;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Repositories;

public class RepositoryExtensionsTests
{
    [Fact]
    public async Task FindAsync_filters_by_predicate()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        IRepository<Vendor> repo = new Repository<Vendor>(ctx);
        await repo.AddAsync(new Vendor { VendorName = "Alpha", TaxCode = "111" });
        await repo.AddAsync(new Vendor { VendorName = "Beta", TaxCode = "222" });
        await ctx.SaveChangesAsync();

        var found = await repo.FindAsync(v => v.TaxCode == "222");

        found.Count.ShouldBe(1);
        found[0].VendorName.ShouldBe("Beta");
    }

    [Fact]
    public async Task ExistsAsync_returns_true_when_match()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        IRepository<Vendor> repo = new Repository<Vendor>(ctx);
        await repo.AddAsync(new Vendor { VendorName = "Alpha" });
        await ctx.SaveChangesAsync();

        (await repo.ExistsAsync(v => v.VendorName == "Alpha")).ShouldBeTrue();
        (await repo.ExistsAsync(v => v.VendorName == "Zzz")).ShouldBeFalse();
    }

    [Fact]
    public async Task UnitOfWork_SaveChanges_persists()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        IRepository<Vendor> repo = new Repository<Vendor>(ctx);
        IUnitOfWork uow = new UnitOfWork(ctx);
        await repo.AddAsync(new Vendor { VendorName = "X" });

        var affected = await uow.SaveChangesAsync();

        affected.ShouldBeGreaterThan(0);
        ctx.Vendors.Count().ShouldBe(1);
    }
}
