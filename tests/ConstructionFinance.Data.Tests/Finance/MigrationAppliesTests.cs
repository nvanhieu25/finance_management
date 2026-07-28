using ConstructionFinance.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Finance;

public class MigrationAppliesTests
{
    [Fact]
    public async Task Migrations_apply_cleanly_and_create_vitri_hierarchy_tables()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"cf_migrate_{Guid.NewGuid():N}.db");
        try
        {
            using (var ctx = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={tempFile}").Options))
            {
                ctx.Database.Migrate();
            }

            using var connection = new SqliteConnection($"Data Source={tempFile}");
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT name FROM sqlite_master
                WHERE type = 'table'
                  AND name IN ('finance_vitris', 'finance_cost_groups', 'finance_packages', 'finance_dots')
                """;
            using var reader = await command.ExecuteReaderAsync();
            var found = 0;
            while (await reader.ReadAsync())
            {
                found++;
            }

            found.ShouldBe(4);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
