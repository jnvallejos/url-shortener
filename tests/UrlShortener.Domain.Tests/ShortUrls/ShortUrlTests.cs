using FluentAssertions;
using UrlShortener.Domain.Exceptions;
using UrlShortener.Domain.ShortUrls;
using UrlShortener.Domain.ShortUrls.Events;

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

    [Fact]
    public void RegisterClick_OnEnabledNotExpired_IncrementsClickCount()
    {
        var shortUrl = ShortUrl.Create(AnyShortCode(), AnyOriginalUrl());

        shortUrl.RegisterClick(DateTime.UtcNow, userAgent: null, ipAddress: null);

        shortUrl.ClickCount.Should().Be(1);
    }

    [Fact]
    public void RegisterClick_AfterMultipleClicks_AccumulatesCount()
    {
        var shortUrl = ShortUrl.Create(AnyShortCode(), AnyOriginalUrl());

        shortUrl.RegisterClick(DateTime.UtcNow, null, null);
        shortUrl.RegisterClick(DateTime.UtcNow, null, null);
        shortUrl.RegisterClick(DateTime.UtcNow, null, null);

        shortUrl.ClickCount.Should().Be(3);
    }

    [Fact]
    public void RegisterClick_OnEnabledNotExpired_RaisesShortUrlClickedEvent()
    {
        var shortUrl = ShortUrl.Create(AnyShortCode(), AnyOriginalUrl());

        shortUrl.RegisterClick(DateTime.UtcNow, "ua", "1.2.3.4");

        shortUrl.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ShortUrlClickedEvent>();
    }

    [Fact]
    public void RegisterClick_RaisedEvent_ContainsShortUrlIdAndShortCode()
    {
        var shortUrl = ShortUrl.Create(AnyShortCode(), AnyOriginalUrl());

        shortUrl.RegisterClick(DateTime.UtcNow, null, null);

        var evt = shortUrl.DomainEvents.OfType<ShortUrlClickedEvent>().Single();
        evt.ShortUrlId.Should().Be(shortUrl.Id);
        evt.ShortCodeValue.Should().Be("abc1234");
    }

    [Fact]
    public void RegisterClick_RaisedEvent_ContainsClickedAtAndUserAgentAndIp()
    {
        var shortUrl = ShortUrl.Create(AnyShortCode(), AnyOriginalUrl());
        var clickedAt = DateTime.UtcNow.AddSeconds(-5);

        shortUrl.RegisterClick(clickedAt, "Mozilla/5.0", "203.0.113.7");

        var evt = shortUrl.DomainEvents.OfType<ShortUrlClickedEvent>().Single();
        evt.ClickedAt.Should().Be(clickedAt);
        evt.UserAgent.Should().Be("Mozilla/5.0");
        evt.IpAddress.Should().Be("203.0.113.7");
    }

    [Fact]
    public void Disable_OnEnabled_SetsIsEnabledFalse()
    {
        var shortUrl = ShortUrl.Create(AnyShortCode(), AnyOriginalUrl());

        shortUrl.Disable();

        shortUrl.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Disable_OnAlreadyDisabled_IsIdempotent()
    {
        var shortUrl = ShortUrl.Create(AnyShortCode(), AnyOriginalUrl());
        shortUrl.Disable();

        Action act = () => shortUrl.Disable();

        act.Should().NotThrow();
        shortUrl.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Enable_OnDisabled_SetsIsEnabledTrue()
    {
        var shortUrl = ShortUrl.Create(AnyShortCode(), AnyOriginalUrl());
        shortUrl.Disable();

        shortUrl.Enable();

        shortUrl.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Enable_OnAlreadyEnabled_IsIdempotent()
    {
        var shortUrl = ShortUrl.Create(AnyShortCode(), AnyOriginalUrl());

        Action act = () => shortUrl.Enable();

        act.Should().NotThrow();
        shortUrl.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void RegisterClick_OnDisabled_ThrowsDomainException()
    {
        var shortUrl = ShortUrl.Create(AnyShortCode(), AnyOriginalUrl());
        shortUrl.Disable();

        Action act = () => shortUrl.RegisterClick(DateTime.UtcNow, null, null);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RegisterClick_OnExpired_ThrowsShortUrlExpiredException()
    {
        var future = DateTime.UtcNow.AddMinutes(5);
        var shortUrl = ShortUrl.Create(AnyShortCode(), AnyOriginalUrl(), expiresAt: future);

        Action act = () => shortUrl.RegisterClick(future.AddSeconds(1), null, null);

        act.Should().Throw<ShortUrlExpiredException>();
    }

    [Fact]
    public void RegisterClick_OnExpired_ExceptionMessageContainsShortCodeAndExpiresAt()
    {
        var future = DateTime.UtcNow.AddMinutes(5);
        var shortUrl = ShortUrl.Create(AnyShortCode(), AnyOriginalUrl(), expiresAt: future);
        var clickedAt = future.AddSeconds(1);

        Action act = () => shortUrl.RegisterClick(clickedAt, null, null);

        act.Should().Throw<ShortUrlExpiredException>()
            .Where(e => e.Message.Contains("abc1234") && e.Message.Contains(future.ToString("O")));
    }
}
