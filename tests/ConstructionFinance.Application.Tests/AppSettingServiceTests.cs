using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Data;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class AppSettingServiceTests
{
    private static IAppSettingService CreateService(AppDbContext ctx) =>
        new AppSettingService(ctx);

    [Fact]
    public async Task GetAsync_returns_null_when_missing()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        (await svc.GetAsync("nonexistent")).ShouldBeNull();
    }

    [Fact]
    public async Task SetAsync_inserts_when_missing()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        await svc.SetAsync("company_name", "Sở Xây dựng Tỉnh ABC");

        (await svc.GetAsync("company_name")).ShouldBe("Sở Xây dựng Tỉnh ABC");
    }

    [Fact]
    public async Task SetAsync_updates_when_exists()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);
        await svc.SetAsync("k", "v1");

        await svc.SetAsync("k", "v2");

        (await svc.GetAsync("k")).ShouldBe("v2");
    }

    [Fact]
    public async Task GetAllAsync_returns_all_key_value_pairs()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);
        await svc.SetAsync("a", "1");
        await svc.SetAsync("b", "2");

        var all = await svc.GetAllAsync();
        all.ShouldContain(s => s.Key == "a" && s.Value == "1");
        all.ShouldContain(s => s.Key == "b" && s.Value == "2");
    }
}
