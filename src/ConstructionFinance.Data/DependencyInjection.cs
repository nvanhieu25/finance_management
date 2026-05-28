using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddDataServices(this IServiceCollection services)
    {
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ICostRepository, CostRepository>();
        services.AddScoped<ICostUnitTypeRepository, CostUnitTypeRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}
