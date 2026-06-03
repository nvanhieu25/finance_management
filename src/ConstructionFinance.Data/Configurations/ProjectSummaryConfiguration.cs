using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class ProjectSummaryConfiguration : IEntityTypeConfiguration<ProjectSummary>
{
    public void Configure(EntityTypeBuilder<ProjectSummary> b)
    {
        b.ToTable("project_summaries");
        b.HasKey(x => x.Id);
        b.Property(x => x.SheetName).IsRequired().HasMaxLength(100);
        b.Property(x => x.ProjectName).IsRequired().HasMaxLength(500);
        b.Property(x => x.ContractorName).HasMaxLength(500);

        b.Property(x => x.ContractValue).HasPrecision(18, 2);
        b.Property(x => x.Advance).HasPrecision(18, 2);
        b.Property(x => x.PaymentKLHT).HasPrecision(18, 2);
        b.Property(x => x.RecoveredAdvance).HasPrecision(18, 2);
        b.Property(x => x.PaymentToContractor).HasPrecision(18, 2);
        b.Property(x => x.UnrecoveredAdvance).HasPrecision(18, 2);
        b.Property(x => x.DisbursedNSNN).HasPrecision(18, 2);
        b.Property(x => x.DisbursedDTNH).HasPrecision(18, 2);
        b.Property(x => x.TotalDisbursed).HasPrecision(18, 2);

        b.HasIndex(x => new { x.ImportBatchId, x.SheetName });
        b.HasMany(x => x.YearlySettlements)
            .WithOne(y => y.ProjectSummary!)
            .HasForeignKey(y => y.ProjectSummaryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
