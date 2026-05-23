using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class CostUnitTypeConfiguration : IEntityTypeConfiguration<CostUnitType>
{
    public void Configure(EntityTypeBuilder<CostUnitType> b)
    {
        b.ToTable("cost_unit_types");
        b.HasKey(x => x.Id);

        b.Property(x => x.Code).IsRequired().HasMaxLength(20);
        b.Property(x => x.Name).IsRequired().HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(255);

        b.HasIndex(x => x.Code).IsUnique();
    }
}
