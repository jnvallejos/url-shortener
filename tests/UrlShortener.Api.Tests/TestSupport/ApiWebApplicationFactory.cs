using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<ApplicationDbContext>));

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(_connection));

            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            ctx.Database.EnsureCreated();
        });
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
