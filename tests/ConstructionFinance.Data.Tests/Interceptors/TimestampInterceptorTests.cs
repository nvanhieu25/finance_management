using ConstructionFinance.Data.Interceptors;
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Interceptors;

public class TimestampInterceptorTests
{
    private static AppDbContext CreateCtxWithInterceptor()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new TimestampInterceptor())
            .Options;
        var ctx = new AppDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public void Insert_sets_CreatedAt_and_UpdatedAt()
    {
        using var ctx = CreateCtxWithInterceptor();
        var before = DateTime.UtcNow.AddSeconds(-1);

        var v = new Vendor { VendorName = "X" };
        ctx.Vendors.Add(v);
        ctx.SaveChanges();

        v.CreatedAt.ShouldBeGreaterThan(before);
        v.UpdatedAt.ShouldBe(v.CreatedAt);
    }

    [Fact]
    public void Update_changes_UpdatedAt_but_not_CreatedAt()
    {
        using var ctx = CreateCtxWithInterceptor();
        var v = new Vendor { VendorName = "X" };
        ctx.Vendors.Add(v);
        ctx.SaveChanges();
        var created = v.CreatedAt;
        Thread.Sleep(20);

        v.VendorName = "Y";
        ctx.SaveChanges();

        v.CreatedAt.ShouldBe(created);
        v.UpdatedAt.ShouldBeGreaterThan(created);
    }
}
