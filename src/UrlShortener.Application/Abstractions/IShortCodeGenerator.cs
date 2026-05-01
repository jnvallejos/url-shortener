using UrlShortener.Domain.ShortUrls;

namespace UrlShortener.Application.Abstractions;

public interface IShortCodeGenerator
{
    Task<ShortCode> GenerateAsync(CancellationToken ct);
}
