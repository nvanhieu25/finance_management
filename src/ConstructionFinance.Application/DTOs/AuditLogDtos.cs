using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Application.DTOs;

public record AuditLogDto(
    int Id,
    string EntityType,
    int EntityId,
    AuditAction Action,
    string? FieldName,
    string? OldValue,
    string? NewValue,
    DateTime ChangedAt);

public record AuditLogFilter(
    string? EntityType,
    DateTime? FromUtc,
    DateTime? ToUtc);
