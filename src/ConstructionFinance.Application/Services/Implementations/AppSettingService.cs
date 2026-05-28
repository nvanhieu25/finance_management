using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Application.Services.Implementations;

public class AppSettingService : IAppSettingService
{
    private readonly IAppSettingRepository _repo;
    private readonly IUnitOfWork _uow;

    public AppSettingService(IAppSettingRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var existing = await _repo.GetByKeyAsync(key, ct);
        return existing?.Value;
    }

    public async Task SetAsync(string key, string? value, string? description = null, CancellationToken ct = default)
    {
        var existing = await _repo.GetByKeyAsync(key, ct);
        if (existing is null)
        {
            await _repo.AddAsync(new AppSetting
            {
                Key = key,
                Value = value,
                Description = description,
                UpdatedAt = DateTime.UtcNow
            }, ct);
        }
        else
        {
            existing.Value = value;
            existing.Description = description ?? existing.Description;
            existing.UpdatedAt = DateTime.UtcNow;
            _repo.Update(existing);
        }
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AppSettingDto>> GetAllAsync(CancellationToken ct = default)
    {
        var all = await _repo.ListAsync(ct);
        return all.Select(s => new AppSettingDto(s.Key, s.Value, s.Description)).ToList();
    }
}
