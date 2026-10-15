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
    // pages read FormInputs from it. Tracked entities would
    // let an unrelated SaveChangesAsync elsewhere in the app flush edits before the
    // user ever asks to save, and would leave a whole tree parked in the change tracker
    // for the app's lifetime every time a project is opened. ReplaceTreeAsync re-queries
    // its own "existing" tracked copy, so nothing here depends on the identity map.
    public async Task<Project?> GetTreeAsync(int id, CancellationToken ct = default) =>
        await _context.FinanceProjects
            .AsNoTracking()
            .Include(p => p.ViTris).ThenInclude(v => v.CostGroups)
                .ThenInclude(g => g.Packages).ThenInclude(pk => pk.Dots)
            .Include(p => p.FormInputs)
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
            .Include(p => p.FormInputs)
            .FirstOrDefaultAsync(p => p.Id == project.Id, ct);
        if (existing is null) return;

        existing.Code = project.Code;
        existing.Name = project.Name;
        existing.Notes = project.Notes;
        UpsertFormInputs(existing, project);

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

    /// Chữ tự nhập đi cùng cây nhưng KHÔNG theo chiến lược xoá-rồi-thêm của ViTris: upsert theo (Form, Key) —
    /// khoá còn thì cập nhật, khoá mới thì thêm, khoá không còn trong bản gửi lên thì xoá. Xoá-rồi-thêm cùng
    /// khoá trong MỘT lần lưu sẽ đụng chỉ mục duy nhất tuỳ thứ tự lệnh EF phát ra.
    private void UpsertFormInputs(Project existing, Project incoming)
    {
        var wanted = incoming.FormInputs
            .GroupBy(i => (i.Form, i.Key))
            .ToDictionary(g => g.Key, g => g.Last().Value);

        foreach (var current in existing.FormInputs.ToList())
        {
            if (wanted.TryGetValue((current.Form, current.Key), out var value))
            {
                current.Value = value;
                wanted.Remove((current.Form, current.Key));
            }
            else
            {
                _context.FormInputs.Remove(current);
            }
        }

        foreach (var ((form, key), value) in wanted)
            existing.FormInputs.Add(new FormInput { ProjectId = existing.Id, Form = form, Key = key, Value = value });
    }
}
