using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations.Finance;

public class CostGroupConfiguration : IEntityTypeConfiguration<CostGroup>
{
    public void Configure(EntityTypeBuilder<CostGroup> b)
    {
        b.ToTable("finance_cost_groups");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(255);
        b.HasIndex(x => new { x.ViTriId, x.SortOrder });
        b.HasOne(x => x.ViTri).WithMany(v => v.CostGroups)
            .HasForeignKey(x => x.ViTriId).OnDelete(DeleteBehavior.Cascade);
    }
}
