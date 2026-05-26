using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Application.Mapping;

public static class AuditLogMappings
{
    public static AuditLogDto ToDto(this AuditLog e) =>
        new(e.Id, e.EntityType, e.EntityId, e.Action, e.FieldName, e.OldValue, e.NewValue, e.ChangedAt);
}
