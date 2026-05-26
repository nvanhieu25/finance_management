using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Data;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class AuditServiceTests
{
    private static IAuditService CreateService(AppDbContext ctx) =>
        new AuditService(new Repository<AuditLog>(ctx));

    [Fact]
    public async Task GetForEntityAsync_returns_logs_of_that_entity_only()
    {
        using var h = new ServiceTestHarness();
        // Creating a vendor produces a Create audit log via interceptor.
        h.Context.Vendors.Add(new Vendor { VendorName = "X" });
        await h.Context.SaveChangesAsync();
        var vendorId = h.Context.Vendors.Single().Id;
        var svc = CreateService(h.Context);

        var logs = await svc.GetForEntityAsync(nameof(Vendor), vendorId);

        logs.ShouldNotBeEmpty();
        logs.ShouldAllBe(l => l.EntityType == nameof(Vendor) && l.EntityId == vendorId);
    }

    [Fact]
    public async Task QueryAsync_filters_by_entity_type()
    {
        using var h = new ServiceTestHarness();
        h.Context.Vendors.Add(new Vendor { VendorName = "X" });
        await h.Context.SaveChangesAsync();
        var svc = CreateService(h.Context);

        var logs = await svc.QueryAsync(new AuditLogFilter(EntityType: nameof(Vendor), FromUtc: null, ToUtc: null));

        logs.ShouldAllBe(l => l.EntityType == nameof(Vendor));
    }
}
