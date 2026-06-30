using ConstructionFinance.Data.Interceptors;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Data;

public static class DependencyInjection
{
    /// Registers AppDbContext on SQLite WITH the timestamp + audit interceptors.
    /// The interceptors are scoped so each context gets its own instance (the audit
    /// interceptor holds per-save state). Use this everywhere the app needs a DbContext.
    public static IServiceCollection AddAppDbContext(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<TimestampInterceptor>();
        services.AddScoped<AuditInterceptor>();
        services.AddDbContext<AppDbContext>((sp, opt) =>
            opt.UseSqlite(connectionString)
               .AddInterceptors(
                   sp.GetRequiredService<TimestampInterceptor>(),
                   sp.GetRequiredService<AuditInterceptor>()));
        return services;
    }

    public static IServiceCollection AddDataServices(this IServiceCollection services)
    {
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ICostRepository, CostRepository>();
        services.AddScoped<ICostUnitTypeRepository, CostUnitTypeRepository>();
        services.AddScoped<IAppSettingRepository, AppSettingRepository>();
        services.AddScoped<IProjectSummaryRepository, ProjectSummaryRepository>();
        services.AddScoped<IFinanceProjectRepository, FinanceProjectRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}
