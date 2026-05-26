using ConstructionFinance.Domain.Common;
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Domain.Entities;

public class AuditLog : BaseEntity
{
    public required string EntityType { get; set; }
    public int EntityId { get; set; }
    public AuditAction Action { get; set; }
    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime ChangedAt { get; set; }
}
