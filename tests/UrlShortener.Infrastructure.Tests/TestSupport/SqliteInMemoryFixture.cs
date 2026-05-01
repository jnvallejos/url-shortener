using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Infrastructure.Persistence;

namespace UrlShortener.Infrastructure.Tests.TestSupport;

public sealed class SqliteInMemoryFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteInMemoryFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = new ApplicationDbContext(Options);
        ctx.Database.EnsureCreated();
    }

    public DbContextOptions<ApplicationDbContext> Options { get; }

    public ApplicationDbContext CreateContext() => new(Options);

    public void Dispose() => _connection.Dispose();
}
