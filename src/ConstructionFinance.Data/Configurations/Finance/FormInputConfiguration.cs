using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations.Finance;

public class FormInputConfiguration : IEntityTypeConfiguration<FormInput>
{
    public void Configure(EntityTypeBuilder<FormInput> b)
    {
        b.ToTable("finance_form_inputs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Form).IsRequired().HasMaxLength(32);
        b.Property(x => x.Key).IsRequired().HasMaxLength(64);
        b.Property(x => x.Value).HasMaxLength(4000);

        b.HasIndex(x => new { x.ProjectId, x.Form, x.Key }).IsUnique();
        b.HasOne(x => x.Project).WithMany(p => p.FormInputs)
            .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
