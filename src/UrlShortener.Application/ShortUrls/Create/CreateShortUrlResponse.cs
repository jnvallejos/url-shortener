namespace UrlShortener.Application.ShortUrls.Create;

public sealed record CreateShortUrlResponse(
    Guid Id,
    string ShortCode,
    string OriginalUrl,
    DateTime? ExpiresAt,
    DateTime CreatedAt);
