using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> b)
    {
        b.ToTable("import_batches");
        b.HasKey(x => x.Id);
        b.Property(x => x.SourceFileName).IsRequired().HasMaxLength(260);
        b.Property(x => x.SourceFileHash).HasMaxLength(64);
        b.Property(x => x.ImportedAtUtc).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => x.SourceFileHash);
        b.HasIndex(x => x.ImportedAtUtc);
        b.HasMany(x => x.Summaries)
            .WithOne(s => s.Batch!)
            .HasForeignKey(s => s.ImportBatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
