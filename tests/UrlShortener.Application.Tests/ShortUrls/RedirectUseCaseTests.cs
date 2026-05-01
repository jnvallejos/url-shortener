using FluentAssertions;
using Moq;
using UrlShortener.Application.Abstractions;
using UrlShortener.Application.ShortUrls.Redirect;
using UrlShortener.Domain.Common;
using UrlShortener.Domain.ShortUrls;
using UrlShortener.Domain.ShortUrls.Events;

namespace UrlShortener.Application.Tests.ShortUrls;

public class RedirectUseCaseTests
{
    private const string ValidCode = "abc1234";
    private const string ValidUrl = "https://example.com/path";
    private const string AnyUserAgent = "Mozilla/5.0";
    private const string AnyIpAddress = "203.0.113.7";

    private readonly Mock<IShortUrlRepository> _repo = new();
    private readonly Mock<IDomainEventDispatcher> _dispatcher = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly List<string> _callLog = new();
    private readonly DateTime _now = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly RedirectUseCase _sut;

    public RedirectUseCaseTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);

        _repo
            .Setup(r => r.UpdateAsync(It.IsAny<ShortUrl>(), It.IsAny<CancellationToken>()))
            .Callback(() => _callLog.Add("Update"))
            .Returns(Task.CompletedTask);

        _repo
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _callLog.Add("Save"))
            .Returns(Task.CompletedTask);

        _dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback(() => _callLog.Add("Dispatch"))
            .Returns(Task.CompletedTask);

        _sut = new RedirectUseCase(_repo.Object, _dispatcher.Object, _clock.Object);
    }

    private static ShortUrl ActiveShortUrl(string code = ValidCode, DateTime? expiresAt = null) =>
        ShortUrl.Create(
            ShortCode.Create(code),
            OriginalUrl.Create(ValidUrl),
            expiresAt);

    private static RedirectRequest Request(
        string code = ValidCode,
        string? userAgent = AnyUserAgent,
        string? ipAddress = AnyIpAddress) =>
        new(code, userAgent, ipAddress);

    [Fact]
    public async Task ExecuteAsync_WithValidEnabledNotExpiredCode_ReturnsSuccessWithOriginalUrl()
    {
        var entity = ActiveShortUrl();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _sut.ExecuteAsync(Request(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.OriginalUrl.Should().Be(ValidUrl);
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_CallsRegisterClickWithClockUtcNowAndUserAgentAndIp()
    {
        var entity = ActiveShortUrl();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        ShortUrlClickedEvent? captured = null;
        _dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<IDomainEvent>, CancellationToken>((events, _) =>
            {
                _callLog.Add("Dispatch");
                captured = events.OfType<ShortUrlClickedEvent>().SingleOrDefault();
            })
            .Returns(Task.CompletedTask);

        await _sut.ExecuteAsync(Request(), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.ClickedAt.Should().Be(_now);
        captured.UserAgent.Should().Be(AnyUserAgent);
        captured.IpAddress.Should().Be(AnyIpAddress);
        entity.ClickCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_DispatchesShortUrlClickedEventAfterSave()
    {
        var entity = ActiveShortUrl();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        IEnumerable<IDomainEvent>? captured = null;
        _dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<IDomainEvent>, CancellationToken>((events, _) =>
            {
                _callLog.Add("Dispatch");
                captured = events.ToList();
            })
            .Returns(Task.CompletedTask);

        await _sut.ExecuteAsync(Request(), CancellationToken.None);

        captured.Should().NotBeNull();
        var clickEvent = captured!.OfType<ShortUrlClickedEvent>().Single();
        clickEvent.ShortCodeValue.Should().Be(ValidCode);
        clickEvent.ClickedAt.Should().Be(_now);
        clickEvent.UserAgent.Should().Be(AnyUserAgent);
        clickEvent.IpAddress.Should().Be(AnyIpAddress);
        _callLog.Should().ContainInConsecutiveOrder("Save", "Dispatch");
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_CallsUpdateAsyncAndSaveChangesAsyncInOrder()
    {
        var entity = ActiveShortUrl();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        await _sut.ExecuteAsync(Request(), CancellationToken.None);

        _callLog.Should().ContainInConsecutiveOrder("Update", "Save");
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_ClearsDomainEventsAfterDispatch()
    {
        var entity = ActiveShortUrl();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        await _sut.ExecuteAsync(Request(), CancellationToken.None);

        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCode_ReturnsFailureWithShortCodeInvalid()
    {
        var result = await _sut.ExecuteAsync(Request(code: "bad"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ShortCode.Invalid");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCode_DoesNotCallRepository()
    {
        await _sut.ExecuteAsync(Request(code: "bad"), CancellationToken.None);

        _repo.Verify(
            r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCodeNotFound_ReturnsFailureWithNotFound()
    {
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortUrl?)null);

        var result = await _sut.ExecuteAsync(Request(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ShortUrl.NotFound");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCodeNotFound_DoesNotCallUpdateOrSaveOrDispatch()
    {
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortUrl?)null);

        await _sut.ExecuteAsync(Request(), CancellationToken.None);

        _repo.Verify(
            r => r.UpdateAsync(It.IsAny<ShortUrl>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repo.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        _dispatcher.Verify(
            d => d.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenShortUrlIsDisabled_ReturnsFailureWithDisabled()
    {
        var entity = ActiveShortUrl();
        entity.Disable();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _sut.ExecuteAsync(Request(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ShortUrl.Disabled");
    }

    [Fact]
    public async Task ExecuteAsync_WhenShortUrlIsDisabled_DoesNotCallSaveOrDispatch()
    {
        var entity = ActiveShortUrl();
        entity.Disable();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        await _sut.ExecuteAsync(Request(), CancellationToken.None);

        _repo.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        _dispatcher.Verify(
            d => d.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenShortUrlIsExpired_ReturnsFailureWithExpired()
    {
        var entity = ActiveShortUrl(expiresAt: _now.AddMinutes(10));
        _clock.SetupGet(c => c.UtcNow).Returns(_now.AddHours(1));
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _sut.ExecuteAsync(Request(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ShortUrl.Expired");
    }

    [Fact]
    public async Task ExecuteAsync_WhenShortUrlIsExpired_DoesNotCallSaveOrDispatch()
    {
        var entity = ActiveShortUrl(expiresAt: _now.AddMinutes(10));
        _clock.SetupGet(c => c.UtcNow).Returns(_now.AddHours(1));
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        await _sut.ExecuteAsync(Request(), CancellationToken.None);

        _repo.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        _dispatcher.Verify(
            d => d.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationRequested_PropagatesOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        Func<Task> act = () => _sut.ExecuteAsync(Request(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
