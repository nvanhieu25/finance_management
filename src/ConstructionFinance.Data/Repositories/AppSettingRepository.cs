using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConstructionFinance.Data.Repositories;

public class AppSettingRepository : IAppSettingRepository
{
    private readonly AppDbContext _context;

    public AppSettingRepository(AppDbContext context) => _context = context;

    public async Task<AppSetting?> GetByKeyAsync(string key, CancellationToken ct = default) =>
        await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);

    public async Task<IReadOnlyList<AppSetting>> ListAsync(CancellationToken ct = default) =>
        await _context.AppSettings.ToListAsync(ct);

    public async Task AddAsync(AppSetting setting, CancellationToken ct = default) =>
        await _context.AppSettings.AddAsync(setting, ct);

    public void Update(AppSetting setting) => _context.AppSettings.Update(setting);
}
