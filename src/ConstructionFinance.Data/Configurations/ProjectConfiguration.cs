using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("projects");
        b.HasKey(x => x.Id);

        b.Property(x => x.ProjectCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.ProjectName).IsRequired().HasMaxLength(255);
        b.Property(x => x.ProjectType).HasMaxLength(100);
        b.Property(x => x.Location).HasMaxLength(500);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.TotalBudget).HasPrecision(18, 2);
        b.Property(x => x.TotalActual).HasPrecision(18, 2);

        b.HasOne(x => x.Contract)
            .WithMany(c => c.Projects)
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.ContractId);
        b.HasIndex(x => new { x.ContractId, x.ProjectCode })
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        b.HasQueryFilter(x => x.DeletedAt == null);
    }
}
