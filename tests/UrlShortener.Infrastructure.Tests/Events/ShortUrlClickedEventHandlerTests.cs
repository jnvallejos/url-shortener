using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Domain.ShortUrls.Events;
using UrlShortener.Infrastructure.Events.Handlers;
using UrlShortener.Infrastructure.Tests.TestSupport;

namespace UrlShortener.Infrastructure.Tests.Events;

public class ShortUrlClickedEventHandlerTests
{
    private const string ValidCode = "abc1234";

    private static ShortUrlClickedEvent BuildEvent(
        Guid? shortUrlId = null,
        string code = ValidCode,
        DateTime? clickedAt = null,
        string? userAgent = "Mozilla/5.0",
        string? ipAddress = "203.0.113.7") =>
        new(
            ShortUrlId:     shortUrlId ?? Guid.NewGuid(),
            ShortCodeValue: code,
            ClickedAt:      clickedAt ?? DateTime.UtcNow,
            UserAgent:      userAgent,
            IpAddress:      ipAddress);

    [Fact]
    public async Task HandleAsync_WithValidEvent_PersistsClickAuditRow()
    {
        using var fixture = new SqliteInMemoryFixture();

        await using (var ctx = fixture.CreateContext())
        {
            var sut = new ShortUrlClickedEventHandler(ctx);
            await sut.HandleAsync(BuildEvent(), CancellationToken.None);
        }

        await using (var ctx = fixture.CreateContext())
        {
            (await ctx.ClickAudits.CountAsync()).Should().Be(1);
        }
    }

    [Fact]
    public async Task HandleAsync_PersistedAudit_ContainsExpectedFields()
    {
        using var fixture = new SqliteInMemoryFixture();
        var shortUrlId = Guid.NewGuid();
        var clickedAt = DateTime.UtcNow;
        var @event = BuildEvent(
            shortUrlId: shortUrlId,
            clickedAt: clickedAt,
            userAgent: "Mozilla/5.0",
            ipAddress: "203.0.113.7");

        await using (var ctx = fixture.CreateContext())
        {
            var sut = new ShortUrlClickedEventHandler(ctx);
            await sut.HandleAsync(@event, CancellationToken.None);
        }

        await using (var ctx = fixture.CreateContext())
        {
            var fetched = await ctx.ClickAudits.SingleAsync();
            fetched.ShortUrlId.Should().Be(shortUrlId);
            fetched.ShortCodeValue.Should().Be(ValidCode);
            fetched.ClickedAt.Should().BeCloseTo(clickedAt, TimeSpan.FromMilliseconds(1));
            fetched.UserAgent.Should().Be("Mozilla/5.0");
            fetched.IpAddress.Should().Be("203.0.113.7");
        }
    }

    [Fact]
    public async Task HandleAsync_WithMultipleEvents_PersistsOneAuditPerEvent()
    {
        using var fixture = new SqliteInMemoryFixture();

        await using (var ctx = fixture.CreateContext())
        {
            var sut = new ShortUrlClickedEventHandler(ctx);
            await sut.HandleAsync(BuildEvent(), CancellationToken.None);
            await sut.HandleAsync(BuildEvent(), CancellationToken.None);
            await sut.HandleAsync(BuildEvent(), CancellationToken.None);
        }

        await using (var ctx = fixture.CreateContext())
        {
            (await ctx.ClickAudits.CountAsync()).Should().Be(3);
        }
    }

    [Fact]
    public async Task HandleAsync_WhenCancellationRequested_PropagatesOperationCanceledException()
    {
        using var fixture = new SqliteInMemoryFixture();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var ctx = fixture.CreateContext();
        var sut = new ShortUrlClickedEventHandler(ctx);

        Func<Task> act = () => sut.HandleAsync(BuildEvent(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
