using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations.Finance;

public class GdnttDeclarationConfiguration : IEntityTypeConfiguration<GdnttDeclaration>
{
    public void Configure(EntityTypeBuilder<GdnttDeclaration> b)
    {
        b.ToTable("finance_gdntt_decl");
        b.HasKey(x => x.Id);
        foreach (var name in new[]
                 {
                     nameof(GdnttDeclaration.So), nameof(GdnttDeclaration.NgayThangNam),
                     nameof(GdnttDeclaration.KinhGui), nameof(GdnttDeclaration.TenDuAnCongTrinh),
                     nameof(GdnttDeclaration.ChuDauTu), nameof(GdnttDeclaration.CanCuQuyetDinh),
                     nameof(GdnttDeclaration.CanCuHopDong), nameof(GdnttDeclaration.CanCuGiayDeNghi),
                     nameof(GdnttDeclaration.ThuocKeHoach), nameof(GdnttDeclaration.TaiNganHang)
                 })
            b.Property<string>(name).HasMaxLength(255);

        b.Property(x => x.ChuyenTienBaoHanh).HasPrecision(18, 2);
        b.Property(x => x.ChuyenTienTamGiu).HasPrecision(18, 2);

        b.HasIndex(x => x.ProjectId).IsUnique();
        b.HasOne(x => x.Project).WithOne(p => p.Gdntt)
            .HasForeignKey<GdnttDeclaration>(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
