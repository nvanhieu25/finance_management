using ConstructionFinance.Application;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplicationServices_resolves_all_services()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite("DataSource=:memory:"));
        services.AddDataServices();
        services.AddApplicationServices();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetService<IVendorService>().ShouldNotBeNull();
        sp.GetService<IDecisionService>().ShouldNotBeNull();
        sp.GetService<IContractService>().ShouldNotBeNull();
        sp.GetService<IProjectService>().ShouldNotBeNull();
        sp.GetService<IPaymentService>().ShouldNotBeNull();
        sp.GetService<ICostService>().ShouldNotBeNull();
        sp.GetService<IFormExcelExporter>().ShouldNotBeNull();
    }
}
