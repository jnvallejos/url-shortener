using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UrlShortener.Infrastructure.Persistence;

namespace UrlShortener.Infrastructure.Tests.Persistence;

public class MigrationApplyTests
{
    [Fact]
    public async Task MigrationsApplyToCleanDatabase_ProducesExpectedSchema()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var ctx = new ApplicationDbContext(options);
        await ctx.Database.MigrateAsync();

        var tables = await GetTableNamesAsync(connection);

        tables.Should().Contain(new[] { "ShortUrls", "ClickAudits" });

        var shortUrlColumns = await GetColumnNamesAsync(connection, "ShortUrls");
        shortUrlColumns.Should().BeEquivalentTo(new[]
        {
            "Id", "ShortCode", "OriginalUrl", "CreatedAt",
            "ExpiresAt", "IsEnabled", "ClickCount",
        });

        var clickAuditColumns = await GetColumnNamesAsync(connection, "ClickAudits");
        clickAuditColumns.Should().BeEquivalentTo(new[]
        {
            "Id", "ShortUrlId", "ShortCodeValue", "ClickedAt", "UserAgent", "IpAddress",
        });
    }

    private static async Task<List<string>> GetTableNamesAsync(SqliteConnection connection)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
        await using var reader = await cmd.ExecuteReaderAsync();
        var names = new List<string>();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }
        return names;
    }

    private static async Task<List<string>> GetColumnNamesAsync(SqliteConnection connection, string table)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table});";
        await using var reader = await cmd.ExecuteReaderAsync();
        var names = new List<string>();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(1));
        }
        return names;
    }
}
