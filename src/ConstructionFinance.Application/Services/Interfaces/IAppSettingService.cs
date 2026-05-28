using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IAppSettingService
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string? value, string? description = null, CancellationToken ct = default);
    Task<IReadOnlyList<AppSettingDto>> GetAllAsync(CancellationToken ct = default);
}
