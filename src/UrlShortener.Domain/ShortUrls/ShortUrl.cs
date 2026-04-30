using UrlShortener.Domain.Common;
using UrlShortener.Domain.Exceptions;
using UrlShortener.Domain.ShortUrls.Events;

namespace UrlShortener.Domain.ShortUrls;

public sealed class ShortUrl : Entity
{
    private ShortUrl(
        Guid id,
        ShortCode shortCode,
        OriginalUrl originalUrl,
        DateTime createdAt,
        DateTime? expiresAt,
        bool isEnabled,
        long clickCount) : base(id)
    {
        ShortCode = shortCode;
        OriginalUrl = originalUrl;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        IsEnabled = isEnabled;
        ClickCount = clickCount;
    }

    public ShortCode ShortCode { get; private set; }
    public OriginalUrl OriginalUrl { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public bool IsEnabled { get; private set; }
    public long ClickCount { get; private set; }

    public static ShortUrl Create(
        ShortCode shortCode,
        OriginalUrl originalUrl,
        DateTime? expiresAt = null)
    {
        var now = DateTime.UtcNow;

        if (expiresAt.HasValue && expiresAt.Value <= now)
        {
            throw new DomainException(
                $"ExpiresAt must be in the future; received '{expiresAt.Value:O}', current UTC '{now:O}'");
        }

        return new ShortUrl(
            id: Guid.NewGuid(),
            shortCode: shortCode,
            originalUrl: originalUrl,
            createdAt: now,
            expiresAt: expiresAt,
            isEnabled: true,
            clickCount: 0);
    }

    public void RegisterClick(DateTime clickedAtUtc, string? userAgent, string? ipAddress)
    {
        ClickCount++;
    }
}
