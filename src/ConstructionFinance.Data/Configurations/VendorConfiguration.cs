using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> b)
    {
        b.ToTable("vendors");
        b.HasKey(x => x.Id);

        b.Property(x => x.VendorCode).HasMaxLength(50);
        b.Property(x => x.VendorName).IsRequired().HasMaxLength(255);
        b.Property(x => x.TaxCode).HasMaxLength(50);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.Phone).HasMaxLength(50);
        b.Property(x => x.Email).HasMaxLength(255);
        b.Property(x => x.ContactPerson).HasMaxLength(255);

        b.HasIndex(x => x.TaxCode)
            .IsUnique()
            .HasFilter("\"TaxCode\" IS NOT NULL AND \"DeletedAt\" IS NULL");

        b.HasQueryFilter(x => x.DeletedAt == null);
    }
}
