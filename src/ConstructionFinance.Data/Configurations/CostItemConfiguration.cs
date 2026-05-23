using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class CostItemConfiguration : IEntityTypeConfiguration<CostItem>
{
    public void Configure(EntityTypeBuilder<CostItem> b)
    {
        b.ToTable("cost_items");
        b.HasKey(x => x.Id);

        b.Property(x => x.ItemCode).HasMaxLength(50);
        b.Property(x => x.ItemName).IsRequired().HasMaxLength(255);
        b.Property(x => x.Path).IsRequired().HasMaxLength(500);
        b.Property(x => x.BidPackage).HasMaxLength(100);
        b.Property(x => x.Quantity).HasPrecision(18, 4);
        b.Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.Property(x => x.EstimatedAmount).HasPrecision(18, 2);

        b.HasOne(x => x.Project)
            .WithMany(p => p.CostItems)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Parent)
            .WithMany(p => p.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.UnitType)
            .WithMany()
            .HasForeignKey(x => x.UnitTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.Vendor)
            .WithMany()
            .HasForeignKey(x => x.VendorId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => x.ProjectId);
        b.HasIndex(x => x.ParentId);
        b.HasIndex(x => new { x.ProjectId, x.ParentId, x.SortOrder });
        b.HasIndex(x => x.Path);

        b.HasQueryFilter(x => x.DeletedAt == null);
    }
}
