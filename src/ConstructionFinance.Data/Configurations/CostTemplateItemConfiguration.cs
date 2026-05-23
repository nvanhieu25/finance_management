using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class CostTemplateItemConfiguration : IEntityTypeConfiguration<CostTemplateItem>
{
    public void Configure(EntityTypeBuilder<CostTemplateItem> b)
    {
        b.ToTable("cost_template_items");
        b.HasKey(x => x.Id);

        b.Property(x => x.ItemCode).HasMaxLength(50);
        b.Property(x => x.ItemName).IsRequired().HasMaxLength(255);

        b.HasOne(x => x.Template)
            .WithMany(t => t.Items)
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Parent)
            .WithMany(p => p.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.DefaultUnit)
            .WithMany()
            .HasForeignKey(x => x.DefaultUnitId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.TemplateId, x.ParentId, x.SortOrder });
    }
}
