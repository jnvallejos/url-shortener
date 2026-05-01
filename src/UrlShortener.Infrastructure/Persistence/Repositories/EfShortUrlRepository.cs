using Microsoft.EntityFrameworkCore;
using UrlShortener.Application.Abstractions;
using UrlShortener.Domain.ShortUrls;

namespace UrlShortener.Infrastructure.Persistence.Repositories;

public sealed class EfShortUrlRepository : IShortUrlRepository
{
    private readonly ApplicationDbContext _db;

    public EfShortUrlRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<ShortUrl?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.ShortUrls.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<ShortUrl?> GetByCodeAsync(ShortCode code, CancellationToken ct) =>
        _db.ShortUrls.FirstOrDefaultAsync(s => s.ShortCode == code, ct);

    public async Task<bool> ExistsByCodeAsync(ShortCode code, CancellationToken ct) =>
        await _db.ShortUrls.AnyAsync(s => s.ShortCode == code, ct);

    public Task AddAsync(ShortUrl shortUrl, CancellationToken ct)
    {
        _db.ShortUrls.Add(shortUrl);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ShortUrl shortUrl, CancellationToken ct)
    {
        // No-op: entities loaded via GetBy*Async are tracked, and EF's change tracker
        // already detects mutations. Calling _db.Update on a tracked entity would risk
        // duplicate-attach errors. Method preserved on the contract for intent and as
        // a hook for future detached-entity scenarios.
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) =>
        _db.SaveChangesAsync(ct);
}
