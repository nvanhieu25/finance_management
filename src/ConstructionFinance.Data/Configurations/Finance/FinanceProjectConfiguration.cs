using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations.Finance;

public class FinanceProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("finance_projects");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.Name).IsRequired().HasMaxLength(255);
        b.Property(x => x.ContractorName).HasMaxLength(255);
        b.Property(x => x.ContractValue).HasPrecision(18, 2);
        b.Property(x => x.ContractAnnexValue).HasPrecision(18, 2);

        b.HasMany(x => x.Lines)
            .WithOne(l => l.Project)
            .HasForeignKey(l => l.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Decisions)
            .WithOne(d => d.Project)
            .HasForeignKey(d => d.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}
