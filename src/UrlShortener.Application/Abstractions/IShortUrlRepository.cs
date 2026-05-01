using UrlShortener.Domain.ShortUrls;

namespace UrlShortener.Application.Abstractions;

public interface IShortUrlRepository
{
    Task<ShortUrl?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<ShortUrl?> GetByCodeAsync(ShortCode code, CancellationToken ct);

    Task<bool> ExistsByCodeAsync(ShortCode code, CancellationToken ct);

    Task AddAsync(ShortUrl shortUrl, CancellationToken ct);

    Task UpdateAsync(ShortUrl shortUrl, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
