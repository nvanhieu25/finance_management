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
    // Insert entries whose EntityId must be read AFTER SaveChanges assigns the autoincrement Id.
    private readonly List<(EntityEntry Entry, AuditLog Log)> _pendingCreates = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FlushPendingCreates(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        FlushPendingCreates(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void Capture(DbContext? ctx)
    {
        if (ctx is null) return;
        var now = DateTime.UtcNow;
        var immediateLogs = new List<AuditLog>();

        foreach (var entry in ctx.ChangeTracker.Entries())
        {
            if (entry.Entity.GetType().GetCustomAttributes(typeof(AuditableAttribute), true).Length == 0)
                continue;
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var entityType = entry.Entity.GetType().Name;

            if (entry.State == EntityState.Added)
            {
                var log = new AuditLog
                {
                    EntityType = entityType,
                    EntityId = 0, // patched in SavedChanges
                    Action = AuditAction.Create,
                    NewValue = SerializeSnapshot(entry),
                    ChangedAt = now
                };
                _pendingCreates.Add((entry, log));
            }
            else if (entry.State == EntityState.Modified)
            {
                var id = GetId(entry);
                if (IsSoftDelete(entry))
                {
                    immediateLogs.Add(new AuditLog
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
                    immediateLogs.Add(new AuditLog
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
                immediateLogs.Add(new AuditLog
                {
                    EntityType = entityType,
                    EntityId = GetId(entry),
                    Action = AuditAction.Delete,
                    OldValue = SerializeSnapshot(entry),
                    ChangedAt = now
                });
            }
        }

        if (immediateLogs.Count > 0)
            ctx.Set<AuditLog>().AddRange(immediateLogs);
    }

    private void FlushPendingCreates(DbContext? ctx)
    {
        if (ctx is null || _pendingCreates.Count == 0) return;
        foreach (var (entry, log) in _pendingCreates)
            log.EntityId = GetId(entry);
        ctx.Set<AuditLog>().AddRange(_pendingCreates.Select(p => p.Log));
        _pendingCreates.Clear();
        // Persist the Create logs after Ids are known. AuditLog itself is not [Auditable],
        // so this nested SaveChanges does not recurse into more pending creates.
        ctx.SaveChanges();
    }

    private static int GetId(EntityEntry entry) =>
        entry.Property("Id").CurrentValue is int id ? id : 0;

    private static bool IsSoftDelete(EntityEntry entry)
    {
        if (entry.Entity is not ISoftDeletable) return false;
        var deletedAt = entry.Property(nameof(ISoftDeletable.DeletedAt));
        return deletedAt.OriginalValue is null && deletedAt.CurrentValue is not null;
    }

    private static string SerializeSnapshot(EntityEntry entry)
    {
        var dict = entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
        return JsonSerializer.Serialize(dict);
    }
}
