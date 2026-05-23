using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class ApprovalDecisionConfiguration : IEntityTypeConfiguration<ApprovalDecision>
{
    public void Configure(EntityTypeBuilder<ApprovalDecision> b)
    {
        b.ToTable("approval_decisions");
        b.HasKey(x => x.Id);

        b.Property(x => x.DecisionCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.DecisionTitle).IsRequired().HasMaxLength(255);
        b.Property(x => x.IssuingAuthority).IsRequired().HasMaxLength(255);
        b.Property(x => x.TotalApprovedAmount).HasPrecision(18, 2);

        b.HasIndex(x => x.DecisionCode)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        b.HasQueryFilter(x => x.DeletedAt == null);
    }
}
