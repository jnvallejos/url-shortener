namespace UrlShortener.Application.ShortUrls.GetByCode;

public sealed record GetShortUrlResponse(
    Guid Id,
    string ShortCode,
    string OriginalUrl,
    DateTime? ExpiresAt,
    DateTime CreatedAt,
    bool IsEnabled,
    long ClickCount);
