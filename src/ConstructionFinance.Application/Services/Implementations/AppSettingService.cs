using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Data;
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConstructionFinance.Application.Services.Implementations;

public class AppSettingService : IAppSettingService
{
    private readonly AppDbContext _db;

    public AppSettingService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var setting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        return setting?.Value;
    }

    public async Task SetAsync(string key, string? value, string? description = null, CancellationToken ct = default)
    {
        var existing = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (existing is null)
        {
            _db.AppSettings.Add(new AppSetting
            {
                Key = key,
                Value = value,
                Description = description,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Value = value;
            existing.Description = description ?? existing.Description;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AppSettingDto>> GetAllAsync(CancellationToken ct = default)
    {
        var all = await _db.AppSettings.ToListAsync(ct);
        return all.Select(s => new AppSettingDto(s.Key, s.Value, s.Description)).ToList();
    }
}
