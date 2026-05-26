using ConstructionFinance.Data;
using ConstructionFinance.Data.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ConstructionFinance.Application.Tests;

/// Spins up a real SQLite in-memory AppDbContext with interceptors,
/// for integration-style service tests (real repositories + UnitOfWork).
public sealed class ServiceTestHarness : IDisposable
{
    private readonly SqliteConnection _connection;
    public AppDbContext Context { get; }

    public ServiceTestHarness()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new TimestampInterceptor(), new AuditInterceptor())
            .Options;
        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
