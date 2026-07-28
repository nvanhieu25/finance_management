using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations.Finance;

public class DotConfiguration : IEntityTypeConfiguration<Dot>
{
    public void Configure(EntityTypeBuilder<Dot> b)
    {
        b.ToTable("finance_dots");
        b.HasKey(x => x.Id);
        b.Property(x => x.Content).HasMaxLength(255);
        b.Property(x => x.DocumentNo).HasMaxLength(255);
        b.Property(x => x.Fund).HasConversion<int>();
        b.Property(x => x.Advance).HasPrecision(18, 2);
        b.Property(x => x.PaymentKLHT).HasPrecision(18, 2);
        b.Property(x => x.RecoveredAdvance).HasPrecision(18, 2);
        b.HasIndex(x => new { x.PackageId, x.SortOrder });
        b.HasOne(x => x.Package).WithMany(pk => pk.Dots)
            .HasForeignKey(x => x.PackageId).OnDelete(DeleteBehavior.Cascade);
    }
}
