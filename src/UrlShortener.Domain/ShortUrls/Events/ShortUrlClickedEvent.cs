using UrlShortener.Domain.Common;

namespace UrlShortener.Domain.ShortUrls.Events;

public sealed record ShortUrlClickedEvent(
    Guid ShortUrlId,
    string ShortCodeValue,
    DateTime ClickedAt,
    string? UserAgent,
    string? IpAddress) : IDomainEvent
{
    public DateTime OccurredOn => ClickedAt;
}
