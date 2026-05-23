using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class CostTemplateConfiguration : IEntityTypeConfiguration<CostTemplate>
{
    public void Configure(EntityTypeBuilder<CostTemplate> b)
    {
        b.ToTable("cost_templates");
        b.HasKey(x => x.Id);

        b.Property(x => x.TemplateName).IsRequired().HasMaxLength(255);
        b.HasIndex(x => x.TemplateName).IsUnique();
    }
}
