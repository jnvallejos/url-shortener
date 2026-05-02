namespace UrlShortener.Api.Contracts;

public sealed record UpdateExpirationContract(DateTime? NewExpiresAt);
