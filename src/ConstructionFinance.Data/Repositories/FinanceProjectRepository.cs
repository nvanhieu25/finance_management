using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ConstructionFinance.Data.Repositories;

public class FinanceProjectRepository : IFinanceProjectRepository
{
    private readonly AppDbContext _context;

    public FinanceProjectRepository(AppDbContext context) => _context = context;

    public async Task<int> AddAsync(Project project, CancellationToken ct = default)
    {
        await _context.FinanceProjects.AddAsync(project, ct);
        await _context.SaveChangesAsync(ct);
        return project.Id;
    }

    public async Task<Project?> GetWithLinesAsync(int id, CancellationToken ct = default) =>
        await _context.FinanceProjects
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        var existing = await _context.FinanceProjects
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == project.Id, ct);
        if (existing is null) return;

        existing.Code = project.Code;
        existing.Name = project.Name;
        existing.ContractorName = project.ContractorName;
        existing.ContractValue = project.ContractValue;
        existing.Notes = project.Notes;

        // Replace strategy: drop all current lines, re-add the incoming ones as
        // fresh rows. Single-user, small data — simplest correct approach and it
        // sidesteps identity-matching of edited rows.
        _context.FinanceProjectLines.RemoveRange(existing.Lines);
        existing.Lines.Clear();
        foreach (var line in project.Lines)
        {
            existing.Lines.Add(new ProjectLineItem
            {
                SortOrder = line.SortOrder,
                Date = line.Date,
                PackageName = line.PackageName,
                ContractRef = line.ContractRef,
                Advance = line.Advance,
                PaymentKLHT = line.PaymentKLHT,
                RecoveredAdvance = line.RecoveredAdvance,
                DisbursedNSNN = line.DisbursedNSNN,
                DisbursedNgoaiHoi = line.DisbursedNgoaiHoi
            });
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default) =>
        await _context.FinanceProjects.OrderBy(p => p.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Project>> ListWithLinesAsync(CancellationToken ct = default) =>
        await _context.FinanceProjects
            .Include(p => p.Lines)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

    // AsNoTracking: this tree is handed straight to the desktop view models, whose form
    // pages mutate the declaration entities via property setters. Tracked entities would
    // let an unrelated SaveChangesAsync elsewhere in the app flush those edits before the
    // user ever asks to save, and would leave a whole tree parked in the change tracker
    // for the app's lifetime every time a project is opened. ReplaceTreeAsync re-queries
    // its own "existing" tracked copy, so nothing here depends on the identity map.
    public async Task<Project?> GetTreeAsync(int id, CancellationToken ct = default) =>
        await _context.FinanceProjects
            .AsNoTracking()
            .Include(p => p.ViTris).ThenInclude(v => v.CostGroups)
                .ThenInclude(g => g.Packages).ThenInclude(pk => pk.Dots)
            .Include(p => p.ThongTri)
            .Include(p => p.KsChi)
            .Include(p => p.Gdntt)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    // AsNoTracking: feeds only the all-projects Tổng hợp nav screen, a read/aggregate path.
    public async Task<IReadOnlyList<Project>> ListTreesAsync(CancellationToken ct = default) =>
        await _context.FinanceProjects
            .AsNoTracking()
            .Include(p => p.ViTris).ThenInclude(v => v.CostGroups)
                .ThenInclude(g => g.Packages).ThenInclude(pk => pk.Dots)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

    public async Task ReplaceTreeAsync(Project project, CancellationToken ct = default)
    {
        if (project.Id == 0)
        {
            await _context.FinanceProjects.AddAsync(project, ct);
            await _context.SaveChangesAsync(ct);
            return;
        }

        var existing = await _context.FinanceProjects
            .Include(p => p.ViTris).ThenInclude(v => v.CostGroups)
                .ThenInclude(g => g.Packages).ThenInclude(pk => pk.Dots)
            .Include(p => p.ThongTri)
            .Include(p => p.KsChi)
            .Include(p => p.Gdntt)
            .FirstOrDefaultAsync(p => p.Id == project.Id, ct);
        if (existing is null) return;

        existing.Code = project.Code;
        existing.Name = project.Name;
        existing.Notes = project.Notes;
        UpsertDeclarations(existing, project);

        // Replace strategy (mirrors UpdateAsync for flat lines): drop the whole
        // existing tree — SQLite's ON DELETE CASCADE takes the CostGroups/Packages/
        // Dots with it — and re-add the incoming ViTris as fresh entities so EF
        // inserts them rather than reusing/duplicating tracked instances.
        _context.ViTris.RemoveRange(existing.ViTris);
        existing.ViTris.Clear();

        foreach (var viTri in project.ViTris)
        {
            var newViTri = new ViTri
            {
                Name = viTri.Name,
                NguonVon = viTri.NguonVon,
                SortOrder = viTri.SortOrder
            };
            foreach (var group in viTri.CostGroups)
            {
                var newGroup = new CostGroup
                {
                    Name = group.Name,
                    SortOrder = group.SortOrder
                };
                foreach (var package in group.Packages)
                {
                    var newPackage = new Package
                    {
                        Name = package.Name,
                        ContractorName = package.ContractorName,
                        BankAccount = package.BankAccount,
                        ContractValue = package.ContractValue,
                        ContractAnnexValue = package.ContractAnnexValue,
                        SortOrder = package.SortOrder
                    };
                    foreach (var dot in package.Dots)
                    {
                        newPackage.Dots.Add(new Dot
                        {
                            SortOrder = dot.SortOrder,
                            Date = dot.Date,
                            Content = dot.Content,
                            DocumentNo = dot.DocumentNo,
                            Fund = dot.Fund,
                            Advance = dot.Advance,
                            PaymentKLHT = dot.PaymentKLHT,
                            RecoveredAdvance = dot.RecoveredAdvance
                        });
                    }
                    newGroup.Packages.Add(newPackage);
                }
                newViTri.CostGroups.Add(newGroup);
            }
            existing.ViTris.Add(newViTri);
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var p = await _context.FinanceProjects.FindAsync(new object[] { id }, ct);
        if (p is not null)
        {
            _context.FinanceProjects.Remove(p);
            await _context.SaveChangesAsync(ct);
        }
    }

    /// Bản khai biểu mẫu đi cùng cây nhưng KHÔNG theo chiến lược xóa-rồi-thêm như
    /// ViTris: nó là hàng 1–1 lâu dài của dự án. Không truyền bản khai nào thì giữ
    /// nguyên bản đang lưu (màn hình workbook có thể lưu trước khi form được mở).
    private void UpsertDeclarations(Project existing, Project incoming)
    {
        if (incoming.ThongTri is not null)
        {
            existing.ThongTri ??= new ThongTriDeclaration { ProjectId = existing.Id };
            CopyThongTri(incoming.ThongTri, existing.ThongTri);
        }
        if (incoming.KsChi is not null)
        {
            existing.KsChi ??= new KsChiDeclaration { ProjectId = existing.Id };
            CopyKsChi(incoming.KsChi, existing.KsChi);
        }
        if (incoming.Gdntt is not null)
        {
            existing.Gdntt ??= new GdnttDeclaration { ProjectId = existing.Id };
            CopyGdntt(incoming.Gdntt, existing.Gdntt);
        }
    }

    private static void CopyThongTri(ThongTriDeclaration from, ThongTriDeclaration to)
    {
        to.SoThongTri = from.SoThongTri;
        to.NgayGhiSo = from.NgayGhiSo;
        to.TenCTMT = from.TenCTMT;
        to.MaSo = from.MaSo;
        to.VeThangNam = from.VeThangNam;
        to.TaiKhoan1 = from.TaiKhoan1;
        to.DonVi1 = from.DonVi1;
        to.SoChungTu1 = from.SoChungTu1;
        to.TaiKhoan2 = from.TaiKhoan2;
        to.DonVi2 = from.DonVi2;
        to.SoChungTu2 = from.SoChungTu2;
        to.LoaiNganSach = from.LoaiNganSach;
        to.Khoan = from.Khoan;
        to.Muc = from.Muc;
        to.TieuMuc = from.TieuMuc;
        to.TietMuc = from.TietMuc;
        to.Nganh = from.Nganh;
    }

    private static void CopyKsChi(KsChiDeclaration from, KsChiDeclaration to)
    {
        to.NgayNhanGiayDeNghi = from.NgayNhanGiayDeNghi;
        to.MucTieuMuc1 = from.MucTieuMuc1;
        to.MucTieuMuc2 = from.MucTieuMuc2;
        to.MucTieuMuc3 = from.MucTieuMuc3;
        to.MucTieuMuc4 = from.MucTieuMuc4;
        to.ThuHoiCacNamTruoc = from.ThuHoiCacNamTruoc;
        to.ThuHoiNamNay = from.ThuHoiNamNay;
        to.ThueGTGT = from.ThueGTGT;
        to.SoTuChoi = from.SoTuChoi;
        to.LyDoTuChoi = from.LyDoTuChoi;
        to.GhiChu = from.GhiChu;
    }

    private static void CopyGdntt(GdnttDeclaration from, GdnttDeclaration to)
    {
        to.So = from.So;
        to.NgayThangNam = from.NgayThangNam;
        to.KinhGui = from.KinhGui;
        to.TenDuAnCongTrinh = from.TenDuAnCongTrinh;
        to.ChuDauTu = from.ChuDauTu;
        to.CanCuQuyetDinh = from.CanCuQuyetDinh;
        to.CanCuHopDong = from.CanCuHopDong;
        to.CanCuGiayDeNghi = from.CanCuGiayDeNghi;
        to.ThuocKeHoach = from.ThuocKeHoach;
        to.TaiNganHang = from.TaiNganHang;
        to.ChuyenTienBaoHanh = from.ChuyenTienBaoHanh;
        to.ChuyenTienTamGiu = from.ChuyenTienTamGiu;
    }
}
