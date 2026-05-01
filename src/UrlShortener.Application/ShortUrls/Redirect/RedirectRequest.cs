namespace UrlShortener.Application.ShortUrls.Redirect;

public sealed record RedirectRequest(
    string Code,
    string? UserAgent,
    string? IpAddress);
