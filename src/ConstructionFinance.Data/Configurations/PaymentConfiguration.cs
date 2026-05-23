using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("payments");
        b.HasKey(x => x.Id);

        b.Property(x => x.PaymentType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Percentage).HasPrecision(5, 2);
        b.Property(x => x.InvoiceNo).HasMaxLength(100);

        b.HasOne(x => x.CostItem)
            .WithMany(c => c.Payments)
            .HasForeignKey(x => x.CostItemId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.CostItemId, x.PaymentNo }).IsUnique();
        b.HasIndex(x => new { x.CostItemId, x.PaymentDate });
    }
}
