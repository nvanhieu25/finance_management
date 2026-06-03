using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class SettlementByYearConfiguration : IEntityTypeConfiguration<SettlementByYear>
{
    public void Configure(EntityTypeBuilder<SettlementByYear> b)
    {
        b.ToTable("settlement_by_years");
        b.HasKey(x => x.Id);
        b.Property(x => x.KLHT).HasPrecision(18, 2);
        b.Property(x => x.Advance).HasPrecision(18, 2);
        b.Property(x => x.NSNN).HasPrecision(18, 2);
        b.Property(x => x.DTNH).HasPrecision(18, 2);
        b.HasIndex(x => new { x.ProjectSummaryId, x.Year });
    }
}
