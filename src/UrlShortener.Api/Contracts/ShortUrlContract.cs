namespace UrlShortener.Api.Contracts;

public sealed record ShortUrlContract(
    Guid Id,
    string ShortCode,
    string OriginalUrl,
    string ShortUrl,
    DateTime? ExpiresAt,
    DateTime CreatedAt,
    bool IsEnabled,
    long ClickCount);
