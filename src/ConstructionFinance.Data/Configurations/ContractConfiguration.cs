using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> b)
    {
        b.ToTable("contracts");
        b.HasKey(x => x.Id);

        b.Property(x => x.ContractCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.ContractName).IsRequired().HasMaxLength(255);
        b.Property(x => x.ContractValue).HasPrecision(18, 2);

        b.HasOne(x => x.ApprovalDecision)
            .WithMany(d => d.Contracts)
            .HasForeignKey(x => x.ApprovalDecisionId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ContractorVendor)
            .WithMany()
            .HasForeignKey(x => x.ContractorVendorId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => x.ApprovalDecisionId);
        b.HasIndex(x => new { x.ApprovalDecisionId, x.ContractCode })
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        b.HasQueryFilter(x => x.DeletedAt == null);
    }
}
