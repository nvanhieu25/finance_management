using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Mapping;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Application.Services.Implementations;

public class AuditService : IAuditService
{
    private readonly IRepository<AuditLog> _logs;

    public AuditService(IRepository<AuditLog> logs) => _logs = logs;

    public async Task<IReadOnlyList<AuditLogDto>> GetForEntityAsync(string entityType, int entityId, CancellationToken ct = default)
    {
        var list = await _logs.FindAsync(l => l.EntityType == entityType && l.EntityId == entityId, ct);
        return list.OrderByDescending(l => l.ChangedAt).Select(l => l.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<AuditLogDto>> QueryAsync(AuditLogFilter filter, CancellationToken ct = default)
    {
        var list = await _logs.FindAsync(l =>
            (filter.EntityType == null || l.EntityType == filter.EntityType) &&
            (filter.FromUtc == null || l.ChangedAt >= filter.FromUtc) &&
            (filter.ToUtc == null || l.ChangedAt <= filter.ToUtc), ct);
        return list.OrderByDescending(l => l.ChangedAt).Select(l => l.ToDto()).ToList();
    }
}
