using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IAuditService
{
    Task<IReadOnlyList<AuditLogDto>> GetForEntityAsync(string entityType, int entityId, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLogDto>> QueryAsync(AuditLogFilter filter, CancellationToken ct = default);
}
