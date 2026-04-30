namespace UrlShortener.Domain.ClickAudits;

public sealed record ClickAudit
{
    public const int MaxUserAgentLength = 512;
    public const int MaxIpAddressLength = 45;

    public Guid Id { get; }
    public Guid ShortUrlId { get; }
    public string ShortCodeValue { get; }
    public DateTime ClickedAt { get; }
    public string? UserAgent { get; }
    public string? IpAddress { get; }

    private ClickAudit(
        Guid id,
        Guid shortUrlId,
        string shortCodeValue,
        DateTime clickedAt,
        string? userAgent,
        string? ipAddress)
    {
        Id = id;
        ShortUrlId = shortUrlId;
        ShortCodeValue = shortCodeValue;
        ClickedAt = clickedAt;
        UserAgent = userAgent;
        IpAddress = ipAddress;
    }

    public static ClickAudit Create(
        Guid shortUrlId,
        string shortCodeValue,
        DateTime clickedAt,
        string? userAgent,
        string? ipAddress)
    {
        return new ClickAudit(
            id: Guid.NewGuid(),
            shortUrlId: shortUrlId,
            shortCodeValue: shortCodeValue,
            clickedAt: clickedAt,
            userAgent: Truncate(userAgent, MaxUserAgentLength),
            ipAddress: Truncate(ipAddress, MaxIpAddressLength));
    }

    private static string? Truncate(string? value, int max) =>
        value is { Length: var len } && len > max ? value[..max] : value;
}
