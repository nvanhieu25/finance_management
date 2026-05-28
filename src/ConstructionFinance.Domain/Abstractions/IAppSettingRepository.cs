using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Domain.Abstractions;

/// Repository for AppSetting (string-keyed key/value store — does not inherit BaseEntity).
public interface IAppSettingRepository
{
    Task<AppSetting?> GetByKeyAsync(string key, CancellationToken ct = default);
    Task<IReadOnlyList<AppSetting>> ListAsync(CancellationToken ct = default);
    Task AddAsync(AppSetting setting, CancellationToken ct = default);
    void Update(AppSetting setting);
}
