using FluentAssertions;
using UrlShortener.Domain.Exceptions;
using UrlShortener.Domain.ShortUrls;

namespace UrlShortener.Domain.Tests.ShortUrls;

public class ShortUrlTests
{
    private static ShortCode AnyShortCode() => ShortCode.Create("abc1234");
    private static OriginalUrl AnyOriginalUrl() => OriginalUrl.Create("https://example.com");

    [Fact]
    public void Create_WithValidInputs_ReturnsShortUrlWithDefaults()
    {
        var shortUrl = ShortUrl.Create(AnyShortCode(), AnyOriginalUrl());

        shortUrl.Should().NotBeNull();
        shortUrl.Id.Should().NotBe(Guid.Empty);
        shortUrl.ShortCode.ToString().Should().Be("abc1234");
        shortUrl.OriginalUrl.ToString().Should().StartWith("https://example.com");
    }

    [Fact]
    public void Create_DefaultsClickCountToZero()
    {
        var shortUrl = ShortUrl.Create(AnyShortCode(), AnyOriginalUrl());

        shortUrl.ClickCount.Should().Be(0);
    }

    [Fact]
    public void Create_DefaultsIsEnabledToTrue()
    {
        var shortUrl = ShortUrl.Create(AnyShortCode(), AnyOriginalUrl());

        shortUrl.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Create_SetsCreatedAtToApproximatelyUtcNow()
    {
        var before = DateTime.UtcNow;
        var shortUrl = ShortUrl.Create(AnyShortCode(), AnyOriginalUrl());
        var after = DateTime.UtcNow;

        shortUrl.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        shortUrl.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Create_WithNullExpiresAt_AllowsCreation()
    {
        var shortUrl = ShortUrl.Create(AnyShortCode(), AnyOriginalUrl(), expiresAt: null);

        shortUrl.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithFutureExpiresAt_AllowsCreation()
    {
        var future = DateTime.UtcNow.AddDays(7);

        var shortUrl = ShortUrl.Create(AnyShortCode(), AnyOriginalUrl(), expiresAt: future);

        shortUrl.ExpiresAt.Should().Be(future);
    }

    [Fact]
    public void Create_WithPastExpiresAt_ThrowsDomainException()
    {
        var past = DateTime.UtcNow.AddDays(-1);

        Action act = () => ShortUrl.Create(AnyShortCode(), AnyOriginalUrl(), expiresAt: past);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WithExpiresAtEqualToNow_ThrowsDomainException()
    {
        // Pass a tiny negative offset to make the assertion deterministic under clock drift.
        var nowish = DateTime.UtcNow.AddMilliseconds(-1);

        Action act = () => ShortUrl.Create(AnyShortCode(), AnyOriginalUrl(), expiresAt: nowish);

        act.Should().Throw<DomainException>();
    }
}
