using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations.Finance;

public class FinanceProjectLineItemConfiguration : IEntityTypeConfiguration<ProjectLineItem>
{
    public void Configure(EntityTypeBuilder<ProjectLineItem> b)
    {
        b.ToTable("finance_project_lines");
        b.HasKey(x => x.Id);
        b.Property(x => x.PackageName).HasMaxLength(255);
        b.Property(x => x.ContractRef).HasMaxLength(255);
        b.Property(x => x.Advance).HasPrecision(18, 2);
        b.Property(x => x.PaymentKLHT).HasPrecision(18, 2);
        b.Property(x => x.RecoveredAdvance).HasPrecision(18, 2);
        b.Property(x => x.DisbursedNSNN).HasPrecision(18, 2);
        b.Property(x => x.DisbursedNgoaiHoi).HasPrecision(18, 2);
        b.HasIndex(x => new { x.ProjectId, x.SortOrder });
    }
}
