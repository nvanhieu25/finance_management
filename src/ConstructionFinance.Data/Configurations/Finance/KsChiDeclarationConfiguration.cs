using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations.Finance;

public class KsChiDeclarationConfiguration : IEntityTypeConfiguration<KsChiDeclaration>
{
    public void Configure(EntityTypeBuilder<KsChiDeclaration> b)
    {
        b.ToTable("finance_kschi_decl");
        b.HasKey(x => x.Id);
        b.Property(x => x.NgayNhanGiayDeNghi).HasMaxLength(255);
        b.Property(x => x.MucTieuMuc1).HasMaxLength(255);
        b.Property(x => x.MucTieuMuc2).HasMaxLength(255);
        b.Property(x => x.MucTieuMuc3).HasMaxLength(255);
        b.Property(x => x.MucTieuMuc4).HasMaxLength(255);
        b.Property(x => x.LyDoTuChoi).HasMaxLength(255);
        b.Property(x => x.GhiChu).HasMaxLength(255);
        b.Property(x => x.ThuHoiCacNamTruoc).HasPrecision(18, 2);
        b.Property(x => x.ThuHoiNamNay).HasPrecision(18, 2);
        b.Property(x => x.ThueGTGT).HasPrecision(18, 2);
        b.Property(x => x.SoTuChoi).HasPrecision(18, 2);

        b.HasIndex(x => x.ProjectId).IsUnique();
        b.HasOne(x => x.Project).WithOne(p => p.KsChi)
            .HasForeignKey<KsChiDeclaration>(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
