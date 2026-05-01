namespace UrlShortener.Application.ShortUrls.Admin.UpdateExpiration;

public sealed record UpdateExpirationResponse(string Code, DateTime? ExpiresAt);
