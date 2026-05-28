using ConstructionFinance.Infrastructure.Backup;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IRestoreService, RestoreService>();
        return services;
    }
}
