namespace UrlShortener.Api.Configuration;

public sealed record RateLimitingOptions
{
    public int PermitLimit { get; init; } = 100;
    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(1);
    public int QueueLimit { get; init; } = 0;
}
