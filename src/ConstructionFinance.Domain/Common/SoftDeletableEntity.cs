namespace ConstructionFinance.Domain.Common;

public abstract class SoftDeletableEntity : BaseEntity, ISoftDeletable
{
    public DateTime? DeletedAt { get; set; }
}
