using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations.Finance;

public class ViTriConfiguration : IEntityTypeConfiguration<ViTri>
{
    public void Configure(EntityTypeBuilder<ViTri> b)
    {
        b.ToTable("finance_vitris");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(255);
        b.Property(x => x.NguonVon).HasMaxLength(255);
        b.HasIndex(x => new { x.ProjectId, x.SortOrder });
        b.HasOne(x => x.Project).WithMany(p => p.ViTris)
            .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
