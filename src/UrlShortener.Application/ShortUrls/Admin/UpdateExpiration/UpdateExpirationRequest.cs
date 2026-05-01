namespace UrlShortener.Application.ShortUrls.Admin.UpdateExpiration;

public sealed record UpdateExpirationRequest(string Code, DateTime? NewExpiresAt);
