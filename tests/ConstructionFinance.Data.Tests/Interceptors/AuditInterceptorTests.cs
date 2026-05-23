using ConstructionFinance.Data.Interceptors;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Interceptors;

public class AuditInterceptorTests
{
    private static AppDbContext CreateCtx()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new TimestampInterceptor(), new AuditInterceptor())
            .Options;
        var ctx = new AppDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public void Insert_creates_one_audit_log_with_action_Create()
    {
        using var ctx = CreateCtx();
        ctx.Vendors.Add(new Vendor { VendorName = "X" });
        ctx.SaveChanges();

        var log = ctx.AuditLogs.Single();
        log.Action.ShouldBe(AuditAction.Create);
        log.EntityType.ShouldBe(nameof(Vendor));
        log.FieldName.ShouldBeNull();
        log.NewValue.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Update_creates_one_audit_log_per_changed_field()
    {
        using var ctx = CreateCtx();
        var v = new Vendor { VendorName = "Old", Address = "A1" };
        ctx.Vendors.Add(v);
        ctx.SaveChanges();
        ctx.AuditLogs.RemoveRange(ctx.AuditLogs);
        ctx.SaveChanges();

        v.VendorName = "New";
        v.Address = "A2";
        ctx.SaveChanges();

        var logs = ctx.AuditLogs.Where(l => l.Action == AuditAction.Update).ToList();
        logs.Count.ShouldBe(2);
        logs.ShouldContain(l => l.FieldName == nameof(Vendor.VendorName) && l.OldValue == "Old" && l.NewValue == "New");
        logs.ShouldContain(l => l.FieldName == nameof(Vendor.Address) && l.OldValue == "A1" && l.NewValue == "A2");
    }

    [Fact]
    public void Soft_delete_creates_audit_log_with_action_Delete()
    {
        using var ctx = CreateCtx();
        var v = new Vendor { VendorName = "X" };
        ctx.Vendors.Add(v);
        ctx.SaveChanges();
        ctx.AuditLogs.RemoveRange(ctx.AuditLogs);
        ctx.SaveChanges();

        v.DeletedAt = DateTime.UtcNow;
        ctx.SaveChanges();

        ctx.AuditLogs.ShouldContain(l => l.Action == AuditAction.Delete && l.EntityType == nameof(Vendor));
    }
}
