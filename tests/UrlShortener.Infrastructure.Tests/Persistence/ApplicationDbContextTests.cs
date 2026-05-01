using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Domain.ClickAudits;
using UrlShortener.Domain.ShortUrls;
using UrlShortener.Infrastructure.Tests.TestSupport;

namespace UrlShortener.Infrastructure.Tests.Persistence;

public class ApplicationDbContextTests
{
    private const string ValidCode = "abc1234";
    private const string OtherCode = "xyz7890";
    private const string ValidUrl = "https://example.com/path";

    private static ShortUrl ActiveShortUrl(string code = ValidCode, DateTime? expiresAt = null) =>
        ShortUrl.Create(
            ShortCode.Create(code),
            OriginalUrl.Create(ValidUrl),
            expiresAt);

    [Fact]
    public async Task SaveChanges_PersistsShortUrl()
    {
        using var fixture = new SqliteInMemoryFixture();
        var entity = ActiveShortUrl();

        await using (var ctx = fixture.CreateContext())
        {
            ctx.ShortUrls.Add(entity);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            (await ctx.ShortUrls.CountAsync()).Should().Be(1);
        }
    }

    [Fact]
    public async Task SaveChanges_PersistsClickAudit()
    {
        using var fixture = new SqliteInMemoryFixture();
        var audit = ClickAudit.Create(
            shortUrlId:     Guid.NewGuid(),
            shortCodeValue: ValidCode,
            clickedAt:      DateTime.UtcNow,
            userAgent:      "ua",
            ipAddress:      "1.2.3.4");

        await using (var ctx = fixture.CreateContext())
        {
            ctx.ClickAudits.Add(audit);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            (await ctx.ClickAudits.CountAsync()).Should().Be(1);
        }
    }

    [Fact]
    public async Task RoundTrip_ShortUrl_PreservesAllFields()
    {
        using var fixture = new SqliteInMemoryFixture();
        var futureExpiry = DateTime.UtcNow.AddHours(1);
        var entity = ActiveShortUrl(expiresAt: futureExpiry);
        entity.RegisterClick(DateTime.UtcNow, "ua", "ip");

        await using (var ctx = fixture.CreateContext())
        {
            ctx.ShortUrls.Add(entity);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var fetched = await ctx.ShortUrls.SingleAsync();
            fetched.Id.Should().Be(entity.Id);
            fetched.ShortCode.ToString().Should().Be(ValidCode);
            fetched.OriginalUrl.ToString().Should().Be(entity.OriginalUrl.ToString());
            fetched.CreatedAt.Should().BeCloseTo(entity.CreatedAt, TimeSpan.FromMilliseconds(1));
            fetched.ExpiresAt.Should().NotBeNull();
            fetched.ExpiresAt!.Value.Should().BeCloseTo(futureExpiry, TimeSpan.FromMilliseconds(1));
            fetched.IsEnabled.Should().BeTrue();
            fetched.ClickCount.Should().Be(1);
        }
    }

    [Fact]
    public async Task RoundTrip_ClickAudit_PreservesAllFields()
    {
        using var fixture = new SqliteInMemoryFixture();
        var clickedAt = DateTime.UtcNow;
        var shortUrlId = Guid.NewGuid();
        var audit = ClickAudit.Create(
            shortUrlId:     shortUrlId,
            shortCodeValue: ValidCode,
            clickedAt:      clickedAt,
            userAgent:      "Mozilla/5.0",
            ipAddress:      "203.0.113.7");

        await using (var ctx = fixture.CreateContext())
        {
            ctx.ClickAudits.Add(audit);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var fetched = await ctx.ClickAudits.SingleAsync();
            fetched.Id.Should().Be(audit.Id);
            fetched.ShortUrlId.Should().Be(shortUrlId);
            fetched.ShortCodeValue.Should().Be(ValidCode);
            fetched.ClickedAt.Should().BeCloseTo(clickedAt, TimeSpan.FromMilliseconds(1));
            fetched.UserAgent.Should().Be("Mozilla/5.0");
            fetched.IpAddress.Should().Be("203.0.113.7");
        }
    }

    [Fact]
    public async Task RoundTrip_ShortCode_ValueObjectMaterialized()
    {
        using var fixture = new SqliteInMemoryFixture();
        var entity = ActiveShortUrl();

        await using (var ctx = fixture.CreateContext())
        {
            ctx.ShortUrls.Add(entity);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var fetched = await ctx.ShortUrls.SingleAsync();
            fetched.ShortCode.Should().BeOfType<ShortCode>();
            fetched.ShortCode.Should().Be(ShortCode.Create(ValidCode));
        }
    }

    [Fact]
    public async Task RoundTrip_OriginalUrl_ValueObjectMaterialized()
    {
        using var fixture = new SqliteInMemoryFixture();
        var entity = ActiveShortUrl();

        await using (var ctx = fixture.CreateContext())
        {
            ctx.ShortUrls.Add(entity);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var fetched = await ctx.ShortUrls.SingleAsync();
            fetched.OriginalUrl.Should().BeOfType<OriginalUrl>();
            fetched.OriginalUrl.Should().Be(OriginalUrl.Create(ValidUrl));
        }
    }

    [Fact]
    public async Task ShortUrl_DuplicateShortCode_RaisesDbUpdateException()
    {
        using var fixture = new SqliteInMemoryFixture();
        var first = ActiveShortUrl();
        var duplicate = ActiveShortUrl();

        await using (var ctx = fixture.CreateContext())
        {
            ctx.ShortUrls.Add(first);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            ctx.ShortUrls.Add(duplicate);
            Func<Task> act = () => ctx.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();
        }
    }

    [Fact]
    public async Task DomainEvents_AfterSave_NotPersisted()
    {
        using var fixture = new SqliteInMemoryFixture();
        var entity = ActiveShortUrl();
        entity.RegisterClick(DateTime.UtcNow, "ua", "ip");

        await using (var ctx = fixture.CreateContext())
        {
            ctx.ShortUrls.Add(entity);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var fetched = await ctx.ShortUrls.SingleAsync();
            fetched.DomainEvents.Should().BeEmpty();
        }
    }
}
