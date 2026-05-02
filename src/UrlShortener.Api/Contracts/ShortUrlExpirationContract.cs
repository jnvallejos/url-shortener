namespace UrlShortener.Api.Contracts;

public sealed record ShortUrlExpirationContract(string Code, DateTime? ExpiresAt);
