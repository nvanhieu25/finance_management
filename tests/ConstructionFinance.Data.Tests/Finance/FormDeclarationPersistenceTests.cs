using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Data.Tests;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Finance;

public class FormDeclarationPersistenceTests
{
    [Fact]
    public async Task ReplaceTreeAsync_inserts_the_three_declarations_and_GetTreeAsync_loads_them()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);
        var id = await repo.AddAsync(new Project { Name = "DA1" });

        await repo.ReplaceTreeAsync(new Project
        {
            Id = id,
            Name = "DA1",
            ThongTri = new ThongTriDeclaration { SoThongTri = "12/TT", Khoan = "11", Muc = "9300" },
            KsChi = new KsChiDeclaration { ThueGTGT = 5_000_000m, LyDoTuChoi = "thiếu hồ sơ" },
            Gdntt = new GdnttDeclaration { TaiNganHang = "Vietcombank Hải Phòng", ChuyenTienBaoHanh = 1_000m }
        });

        // GetTreeAsync is now AsNoTracking; without Clear() the identity map would hand
        // back the very same tracked instances ReplaceTreeAsync just wrote through,
        // which would satisfy these asserts even if the DB round-trip were broken.
        ctx.ChangeTracker.Clear();
        var tree = await repo.GetTreeAsync(id);

        tree!.ThongTri!.SoThongTri.ShouldBe("12/TT");
        tree.ThongTri.Khoan.ShouldBe("11");
        tree.ThongTri.Muc.ShouldBe("9300");
        tree.KsChi!.ThueGTGT.ShouldBe(5_000_000m);
        tree.KsChi.LyDoTuChoi.ShouldBe("thiếu hồ sơ");
        tree.Gdntt!.TaiNganHang.ShouldBe("Vietcombank Hải Phòng");
        tree.Gdntt.ChuyenTienBaoHanh.ShouldBe(1_000m);
    }

    [Fact]
    public async Task ReplaceTreeAsync_updates_an_existing_declaration_in_place()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);
        var id = await repo.AddAsync(new Project { Name = "DA1" });

        await repo.ReplaceTreeAsync(new Project
        {
            Id = id, Name = "DA1",
            ThongTri = new ThongTriDeclaration { SoThongTri = "cũ" }
        });
        await repo.ReplaceTreeAsync(new Project
        {
            Id = id, Name = "DA1",
            ThongTri = new ThongTriDeclaration { SoThongTri = "mới" }
        });

        // GetTreeAsync is now AsNoTracking; without Clear() the identity map would hand
        // back the very same tracked instances ReplaceTreeAsync just wrote through,
        // which would satisfy these asserts even if the DB round-trip were broken.
        ctx.ChangeTracker.Clear();
        var tree = await repo.GetTreeAsync(id);
        tree!.ThongTri!.SoThongTri.ShouldBe("mới");
        ctx.ThongTriDeclarations.Count(d => d.ProjectId == id).ShouldBe(1);
    }

    [Fact]
    public async Task ReplaceTreeAsync_keeps_the_stored_declaration_when_none_is_supplied()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);
        var id = await repo.AddAsync(new Project { Name = "DA1" });

        await repo.ReplaceTreeAsync(new Project
        {
            Id = id, Name = "DA1",
            ThongTri = new ThongTriDeclaration { SoThongTri = "12/TT" }
        });
        await repo.ReplaceTreeAsync(new Project { Id = id, Name = "DA1" });   // không kèm bản khai

        // GetTreeAsync is now AsNoTracking; without Clear() the identity map would hand
        // back the very same tracked instances ReplaceTreeAsync just wrote through,
        // which would satisfy these asserts even if the DB round-trip were broken.
        ctx.ChangeTracker.Clear();
        var tree = await repo.GetTreeAsync(id);
        tree!.ThongTri!.SoThongTri.ShouldBe("12/TT");
    }
}
