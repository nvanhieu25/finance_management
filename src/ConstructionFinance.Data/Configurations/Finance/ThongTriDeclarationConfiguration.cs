using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations.Finance;

public class ThongTriDeclarationConfiguration : IEntityTypeConfiguration<ThongTriDeclaration>
{
    public void Configure(EntityTypeBuilder<ThongTriDeclaration> b)
    {
        b.ToTable("finance_thongtri_decl");
        b.HasKey(x => x.Id);
        foreach (var name in new[]
                 {
                     nameof(ThongTriDeclaration.SoThongTri), nameof(ThongTriDeclaration.NgayGhiSo),
                     nameof(ThongTriDeclaration.TenCTMT), nameof(ThongTriDeclaration.MaSo),
                     nameof(ThongTriDeclaration.VeThangNam), nameof(ThongTriDeclaration.TaiKhoan1),
                     nameof(ThongTriDeclaration.DonVi1), nameof(ThongTriDeclaration.SoChungTu1),
                     nameof(ThongTriDeclaration.TaiKhoan2), nameof(ThongTriDeclaration.DonVi2),
                     nameof(ThongTriDeclaration.SoChungTu2), nameof(ThongTriDeclaration.LoaiNganSach),
                     nameof(ThongTriDeclaration.Khoan), nameof(ThongTriDeclaration.Muc),
                     nameof(ThongTriDeclaration.TieuMuc), nameof(ThongTriDeclaration.TietMuc),
                     nameof(ThongTriDeclaration.Nganh)
                 })
            b.Property<string>(name).HasMaxLength(255);

        b.HasIndex(x => x.ProjectId).IsUnique();
        b.HasOne(x => x.Project).WithOne(p => p.ThongTri)
            .HasForeignKey<ThongTriDeclaration>(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
