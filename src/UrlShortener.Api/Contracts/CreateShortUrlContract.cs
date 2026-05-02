namespace UrlShortener.Api.Contracts;

public sealed record CreateShortUrlContract(
    string OriginalUrl,
    string? CustomCode,
    DateTime? ExpiresAt);
