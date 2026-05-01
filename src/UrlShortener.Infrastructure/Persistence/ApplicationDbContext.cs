using Microsoft.EntityFrameworkCore;
using UrlShortener.Domain.ClickAudits;
using UrlShortener.Domain.ShortUrls;

namespace UrlShortener.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<ShortUrl> ShortUrls => Set<ShortUrl>();
    public DbSet<ClickAudit> ClickAudits => Set<ClickAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
