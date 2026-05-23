using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests;

public class AppDbContextTests
{
    [Fact]
    public void AppDbContext_exposes_all_DbSets()
    {
        using var ctx = TestDbContextFactory.CreateInMemory();

        ctx.ApprovalDecisions.ShouldNotBeNull();
        ctx.Contracts.ShouldNotBeNull();
        ctx.Projects.ShouldNotBeNull();
        ctx.CostItems.ShouldNotBeNull();
        ctx.Payments.ShouldNotBeNull();
        ctx.Vendors.ShouldNotBeNull();
        ctx.CostUnitTypes.ShouldNotBeNull();
        ctx.Attachments.ShouldNotBeNull();
        ctx.AuditLogs.ShouldNotBeNull();
        ctx.CostTemplates.ShouldNotBeNull();
        ctx.CostTemplateItems.ShouldNotBeNull();
        ctx.AppSettings.ShouldNotBeNull();
    }

    [Fact]
    public void Can_insert_and_query_a_Vendor()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        ctx.Vendors.Add(new Vendor { VendorName = "ABC Ltd" });
        ctx.SaveChanges();

        var fetched = ctx.Vendors.AsNoTracking().Single();
        fetched.VendorName.ShouldBe("ABC Ltd");
        fetched.Id.ShouldBeGreaterThan(0);
    }
}
