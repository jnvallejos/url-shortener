namespace UrlShortener.Application.ShortUrls.Create;

public sealed record CreateShortUrlRequest(
    string OriginalUrl,
    DateTime? ExpiresAt,
    string? CustomCode);
