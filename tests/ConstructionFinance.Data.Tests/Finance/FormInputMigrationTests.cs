using ConstructionFinance.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Finance;

/// Chạy migration thật trên tệp SQLite thật: dừng ở migration ngay trước, nhét dữ liệu bằng SQL thô vào ba bảng
/// cũ, rồi chạy `AddFormInputs` và đọc lại. App chạy MigrateAsync mỗi lần khởi động và xoá bảng là không đảo
/// ngược — nên đường này phải được thử trên dữ liệu thật dạng, không chỉ trên model.
public class FormInputMigrationTests : IDisposable
{
    private const string Before = "20260812180157_AddFormDeclarations";
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"cf-mig-{Guid.NewGuid():N}.db");

    private AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={_path};Foreign Keys=True").Options);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
            if (File.Exists(file)) File.Delete(file);
    }

    private async Task MigrateTo(string? target)
    {
        using var ctx = NewContext();
        var migrator = ctx.GetService<IMigrator>();
        if (target is null) await migrator.MigrateAsync();
        else await migrator.MigrateAsync(target);
    }

    private static async Task Exec(AppDbContext ctx, string sql) => await ctx.Database.ExecuteSqlRawAsync(sql);

    private async Task<List<(int ProjectId, string Form, string Key, string? Value)>> ReadInputs()
    {
        using var ctx = NewContext();
        var rows = new List<(int, string, string, string?)>();
        await ctx.Database.OpenConnectionAsync();
        using var command = ctx.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT ProjectId, Form, Key, Value FROM finance_form_inputs ORDER BY ProjectId, Form, Key";
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
        return rows;
    }

    /// `legacyColumns`: ở các migration trước RemoveFlatModel bảng còn ContractValue/ContractAnnexValue (NOT NULL).
    private async Task<int> InsertProject(AppDbContext ctx, string name, bool legacyColumns = true)
    {
        await Exec(ctx, legacyColumns
            ? "INSERT INTO finance_projects (Name, ContractValue, ContractAnnexValue, CreatedAt, UpdatedAt) " +
              $"VALUES ('{name}', '0', '0', '2026-01-01', '2026-01-01')"
            : "INSERT INTO finance_projects (Name, CreatedAt, UpdatedAt) " +
              $"VALUES ('{name}', '2026-01-01', '2026-01-01')");
        using var command = ctx.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT last_insert_rowid()";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Migration_copies_non_empty_values_only()
    {
        await MigrateTo(Before);
        int project;
        using (var ctx = NewContext())
        {
            await ctx.Database.OpenConnectionAsync();
            project = await InsertProject(ctx, "DA1");
            await Exec(ctx, "INSERT INTO finance_thongtri_decl (ProjectId, SoThongTri, Khoan, Muc, CreatedAt, UpdatedAt) " +
                            $"VALUES ({project}, '12/TT', '', NULL, '2026-01-01', '2026-01-01')");
            await Exec(ctx, "INSERT INTO finance_kschi_decl (ProjectId, MucTieuMuc1, ThueGTGT, SoTuChoi, ThuHoiNamNay, ThuHoiCacNamTruoc, " +
                            "CreatedAt, UpdatedAt) " +
                            $"VALUES ({project}, '9300 / 9301', '5000000', '0', '1500000.5', '0', '2026-01-01', '2026-01-01')");
            await Exec(ctx, "INSERT INTO finance_gdntt_decl (ProjectId, TaiNganHang, NgayThangNam, ChuyenTienBaoHanh, ChuyenTienTamGiu, " +
                            "CreatedAt, UpdatedAt) " +
                            $"VALUES ({project}, 'Vietcombank', '3 tháng 4', '1000', '0', '2026-01-01', '2026-01-01')");
        }

        await MigrateTo(null);
        var inputs = await ReadInputs();

        inputs.Select(i => (i.Form, i.Key, i.Value)).ShouldBe(new (string, string, string?)[]
        {
            ("gdntt", "ChuyenTienBaoHanh", "1000"),
            ("gdntt", "NgayThangNam", "Hải phòng, ngày 3 tháng 4"),
            ("gdntt", "TaiNganHang", "Vietcombank"),
            ("kschi", "Muc1", "9300 / 9301"),
            ("kschi", "ThuHoiNamNay", "1500000.5"),
            ("kschi", "ThueGTGT", "5000000"),
            ("thongtri", "SoThongTri", "12/TT"),
        }, ignoreOrder: false);
        inputs.ShouldAllBe(i => i.ProjectId == project);
    }

    [Fact]
    public async Task Migration_runs_on_an_empty_database()
    {
        await MigrateTo(null);

        (await ReadInputs()).ShouldBeEmpty();
        using var ctx = NewContext();
        (await ctx.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Migration_drops_the_three_old_declaration_tables()
    {
        await MigrateTo(null);

        using var ctx = NewContext();
        await ctx.Database.OpenConnectionAsync();
        using var command = ctx.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'finance_%_decl'";
        using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Migration_keeps_the_unique_index_on_project_form_key()
    {
        await MigrateTo(null);
        using var ctx = NewContext();
        await ctx.Database.OpenConnectionAsync();
        var project = await InsertProject(ctx, "DA1", legacyColumns: false);
        await Exec(ctx, "INSERT INTO finance_form_inputs (ProjectId, Form, Key, Value, CreatedAt, UpdatedAt) " +
                        $"VALUES ({project}, 'thongtri', 'A', '1', '2026-01-01', '2026-01-01')");

        var duplicate = async () => await Exec(ctx, "INSERT INTO finance_form_inputs (ProjectId, Form, Key, Value, CreatedAt, UpdatedAt) " +
                                                    $"VALUES ({project}, 'thongtri', 'A', '2', '2026-01-01', '2026-01-01')");

        await duplicate.ShouldThrowAsync<Exception>();
    }
}
