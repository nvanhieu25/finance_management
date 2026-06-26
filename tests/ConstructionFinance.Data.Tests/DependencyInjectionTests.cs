using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddAppDbContext_wires_timestamp_interceptor()
    {
        // Regression for: interceptors were implemented but never registered in the app,
        // so BaseEntity.CreatedAt/UpdatedAt were left at default(DateTime).
        var dbPath = Path.Combine(Path.GetTempPath(), $"itc-{Guid.NewGuid():N}.db");
        try
        {
            using var sp = new ServiceCollection()
                .AddAppDbContext($"Data Source={dbPath}")
                .BuildServiceProvider();

            using var scope = sp.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ctx.Database.EnsureCreated();

            var v = new Vendor { VendorName = "X" };
            ctx.Vendors.Add(v);
            ctx.SaveChanges();

            v.CreatedAt.ShouldNotBe(default);
            v.UpdatedAt.ShouldBe(v.CreatedAt);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
}
