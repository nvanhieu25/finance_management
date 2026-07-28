using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations.Finance;

public class PackageConfiguration : IEntityTypeConfiguration<Package>
{
    public void Configure(EntityTypeBuilder<Package> b)
    {
        b.ToTable("finance_packages");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(255);
        b.Property(x => x.ContractorName).HasMaxLength(255);
        b.Property(x => x.BankAccount).HasMaxLength(255);
        b.Property(x => x.ContractValue).HasPrecision(18, 2);
        b.Property(x => x.ContractAnnexValue).HasPrecision(18, 2);
        b.HasIndex(x => new { x.CostGroupId, x.SortOrder });
        b.HasOne(x => x.CostGroup).WithMany(g => g.Packages)
            .HasForeignKey(x => x.CostGroupId).OnDelete(DeleteBehavior.Cascade);
    }
}
