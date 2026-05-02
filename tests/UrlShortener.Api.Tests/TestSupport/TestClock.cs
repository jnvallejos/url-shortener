using UrlShortener.Application.Abstractions;

namespace UrlShortener.Api.Tests.TestSupport;

public sealed class TestClock : IDateTimeProvider
{
    private DateTime? _override;

    public DateTime UtcNow => _override ?? DateTime.UtcNow;

    public void SetUtcNow(DateTime utcNow) => _override = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);

    public void Reset() => _override = null;
}
