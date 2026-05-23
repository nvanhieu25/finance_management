using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> b)
    {
        b.ToTable("attachments");
        b.HasKey(x => x.Id);

        b.Property(x => x.EntityType).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.FileName).IsRequired().HasMaxLength(255);
        b.Property(x => x.FilePath).IsRequired().HasMaxLength(500);
        b.Property(x => x.MimeType).HasMaxLength(100);

        b.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}
