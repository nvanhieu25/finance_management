using ConstructionFinance.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Finance;

/// Migration `RemoveFlatModel` (7E) xoá mô hình lưới phẳng 6A và đường nhập Excel cũ. App chạy MigrateAsync mỗi
/// lần khởi động và drop bảng là không đảo ngược, nên đường này được thử trên tệp SQLite thật có dữ liệu.
public class RemoveFlatModelMigrationTests : IDisposable
{
    private const string Before = "20261015190720_AddFormInputs";
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"cf-7e-{Guid.NewGuid():N}.db");

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

    private async Task<List<string>> Query(string sql)
    {
        using var ctx = NewContext();
        await ctx.Database.OpenConnectionAsync();
        using var command = ctx.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync()) rows.Add(reader.GetValue(0)?.ToString() ?? "");
        return rows;
    }

    [Fact]
    public async Task Migration_drops_the_five_flat_tables_and_the_three_contract_columns_but_keeps_the_project()
    {
        await MigrateTo(Before);
        using (var ctx = NewContext())
        {
            await ctx.Database.OpenConnectionAsync();
            await ctx.Database.ExecuteSqlRawAsync(
                "INSERT INTO finance_projects (Name, ContractorName, ContractValue, ContractAnnexValue, CreatedAt, UpdatedAt) " +
                "VALUES ('DA1', 'Cty A', '1000', '0', '2026-01-01', '2026-01-01')");
        }

        await MigrateTo(null);

        (await Query("SELECT name FROM sqlite_master WHERE type='table' AND name IN " +
                     "('finance_project_lines','finance_project_decisions','import_batches','project_summaries','settlement_by_years')"))
            .ShouldBeEmpty();
        (await Query("SELECT name FROM pragma_table_info('finance_projects') ORDER BY name"))
            .ShouldBe(new[] { "Code", "CreatedAt", "Id", "Name", "Notes", "UpdatedAt" });
        (await Query("SELECT Name FROM finance_projects")).ShouldBe(new[] { "DA1" });
    }

    [Fact]
    public async Task Foreign_keys_and_unique_indexes_of_the_tree_tables_survive()
    {
        await MigrateTo(null);

        // Bốn khoá ngoại con của finance_projects đáng giữ nay là: finance_vitris và finance_form_inputs.
        (await Query("SELECT \"table\" FROM pragma_foreign_key_list('finance_vitris')")).ShouldContain("finance_projects");
        (await Query("SELECT \"table\" FROM pragma_foreign_key_list('finance_form_inputs')")).ShouldContain("finance_projects");
        (await Query("SELECT name FROM pragma_index_list('finance_form_inputs') WHERE \"unique\" = 1")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Migration_runs_on_an_empty_database_and_leaves_no_pending_migration()
    {
        await MigrateTo(null);

        using var ctx = NewContext();
        (await ctx.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
    }
}
