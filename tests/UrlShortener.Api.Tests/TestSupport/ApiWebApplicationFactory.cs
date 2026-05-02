using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UrlShortener.Application.Abstractions;
using UrlShortener.Domain.ShortUrls;
using UrlShortener.Infrastructure.Persistence;

namespace UrlShortener.Api.Tests.TestSupport;

public class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public ApiWebApplicationFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public TestClock Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<ApplicationDbContext>));

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(_connection));

            services.RemoveAll(typeof(IDateTimeProvider));
            services.AddSingleton<IDateTimeProvider>(Clock);

            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            ctx.Database.EnsureCreated();
        });
    }

    public async Task<ShortUrl> SeedShortUrlAsync(
        string code,
        string originalUrl = "https://example.com/seed",
        DateTime? expiresAt = null,
        bool isEnabled = true)
    {
        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var shortUrl = ShortUrl.Create(
            ShortCode.Create(code),
            OriginalUrl.Create(originalUrl),
            expiresAt);

        if (!isEnabled) shortUrl.Disable();

        ctx.ShortUrls.Add(shortUrl);
        await ctx.SaveChangesAsync();
        return shortUrl;
    }

    public async Task<TResult> WithDbContextAsync<TResult>(Func<ApplicationDbContext, Task<TResult>> action)
    {
        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(ctx);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection.Dispose();
        }
        base.Dispose(disposing);
    }
}
