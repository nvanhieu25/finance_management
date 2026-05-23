using System.Text.Json;
using ConstructionFinance.Domain.Common;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ConstructionFinance.Data.Interceptors;

public class AuditInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CaptureAudit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CaptureAudit(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void CaptureAudit(DbContext? ctx)
    {
        if (ctx is null) return;
        var now = DateTime.UtcNow;
        var logs = new List<AuditLog>();

        foreach (var entry in ctx.ChangeTracker.Entries())
        {
            if (entry.Entity.GetType().GetCustomAttributes(typeof(AuditableAttribute), true).Length == 0)
                continue;
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var entityType = entry.Entity.GetType().Name;
            var id = entry.Property("Id").CurrentValue is int idVal ? idVal : 0;

            if (entry.State == EntityState.Added)
            {
                logs.Add(new AuditLog
                {
                    EntityType = entityType,
                    EntityId = id,
                    Action = AuditAction.Create,
                    NewValue = SerializeSnapshot(entry),
                    ChangedAt = now
                });
            }
            else if (entry.State == EntityState.Modified)
            {
                if (IsSoftDelete(entry))
                {
                    logs.Add(new AuditLog
                    {
                        EntityType = entityType,
                        EntityId = id,
                        Action = AuditAction.Delete,
                        OldValue = SerializeSnapshot(entry),
                        ChangedAt = now
                    });
                    continue;
                }
                foreach (var prop in entry.Properties.Where(p => p.IsModified))
                {
                    if (prop.Metadata.Name == nameof(BaseEntity.UpdatedAt)) continue;
                    logs.Add(new AuditLog
                    {
                        EntityType = entityType,
                        EntityId = id,
                        Action = AuditAction.Update,
                        FieldName = prop.Metadata.Name,
                        OldValue = prop.OriginalValue?.ToString(),
                        NewValue = prop.CurrentValue?.ToString(),
                        ChangedAt = now
                    });
                }
            }
            else if (entry.State == EntityState.Deleted)
            {
                logs.Add(new AuditLog
                {
                    EntityType = entityType,
                    EntityId = id,
                    Action = AuditAction.Delete,
                    OldValue = SerializeSnapshot(entry),
                    ChangedAt = now
                });
            }
        }

        if (logs.Count > 0)
            ctx.Set<AuditLog>().AddRange(logs);
    }

    private static bool IsSoftDelete(EntityEntry entry)
    {
        if (entry.Entity is not ISoftDeletable) return false;
        var deletedAt = entry.Property(nameof(ISoftDeletable.DeletedAt));
        var wasNull = deletedAt.OriginalValue is null;
        var isNotNull = deletedAt.CurrentValue is not null;
        return wasNull && isNotNull;
    }

    private static string SerializeSnapshot(EntityEntry entry)
    {
        var dict = entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
        return JsonSerializer.Serialize(dict);
    }
}
