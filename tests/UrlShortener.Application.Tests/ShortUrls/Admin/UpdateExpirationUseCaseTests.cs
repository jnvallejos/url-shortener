using FluentAssertions;
using Moq;
using UrlShortener.Application.Abstractions;
using UrlShortener.Application.ShortUrls.Admin.UpdateExpiration;
using UrlShortener.Domain.Common;
using UrlShortener.Domain.ShortUrls;

namespace UrlShortener.Application.Tests.ShortUrls.Admin;

public class UpdateExpirationUseCaseTests
{
    private const string ValidCode = "abc1234";
    private const string ValidUrl = "https://example.com/path";

    private readonly Mock<IShortUrlRepository> _repo = new();
    private readonly Mock<IDomainEventDispatcher> _dispatcher = new();
    private readonly List<string> _callLog = new();
    private readonly UpdateExpirationUseCase _sut;

    public UpdateExpirationUseCaseTests()
    {
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

        _sut = new UpdateExpirationUseCase(_repo.Object, _dispatcher.Object);
    }

    private static ShortUrl ActiveShortUrl(string code = ValidCode, DateTime? expiresAt = null) =>
        ShortUrl.Create(
            ShortCode.Create(code),
            OriginalUrl.Create(ValidUrl),
            expiresAt);

    private static DateTime FutureUtc(int minutesFromNow = 60) =>
        DateTime.UtcNow.AddMinutes(minutesFromNow);

    [Fact]
    public async Task ExecuteAsync_WithValidExistingCode_ReturnsSuccess()
    {
        var entity = ActiveShortUrl();
        var newExpiry = FutureUtc();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _sut.ExecuteAsync(
            new UpdateExpirationRequest(ValidCode, newExpiry),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be(ValidCode);
        result.Value.ExpiresAt.Should().Be(newExpiry);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCode_ReturnsFailureWithShortCodeInvalid()
    {
        var result = await _sut.ExecuteAsync(
            new UpdateExpirationRequest("bad", FutureUtc()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ShortCode.Invalid");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCodeNotFound_ReturnsFailureWithNotFound()
    {
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortUrl?)null);

        var result = await _sut.ExecuteAsync(
            new UpdateExpirationRequest(ValidCode, FutureUtc()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ShortUrl.NotFound");
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_CallsUpdateAsyncAndSaveChangesAsyncInOrder()
    {
        var entity = ActiveShortUrl();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        await _sut.ExecuteAsync(
            new UpdateExpirationRequest(ValidCode, FutureUtc()),
            CancellationToken.None);

        _callLog.Should().ContainInConsecutiveOrder("Update", "Save");
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_DispatchesEventsAfterSave()
    {
        var entity = ActiveShortUrl();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        await _sut.ExecuteAsync(
            new UpdateExpirationRequest(ValidCode, FutureUtc()),
            CancellationToken.None);

        _callLog.Should().ContainInConsecutiveOrder("Save", "Dispatch");
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_ClearsDomainEventsAfterDispatch()
    {
        var entity = ActiveShortUrl();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        await _sut.ExecuteAsync(
            new UpdateExpirationRequest(ValidCode, FutureUtc()),
            CancellationToken.None);

        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_ResponseFieldsMatchEntity()
    {
        var entity = ActiveShortUrl();
        var newExpiry = FutureUtc();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _sut.ExecuteAsync(
            new UpdateExpirationRequest(ValidCode, newExpiry),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be(entity.ShortCode.ToString());
        result.Value.ExpiresAt.Should().Be(entity.ExpiresAt);
    }

    [Fact]
    public async Task ExecuteAsync_WithPastExpiration_ReturnsFailureWithInvalidExpiration()
    {
        var entity = ActiveShortUrl();
        var pastExpiry = DateTime.UtcNow.AddMinutes(-5);
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _sut.ExecuteAsync(
            new UpdateExpirationRequest(ValidCode, pastExpiry),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.InvalidExpiration");
        _repo.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullExpiration_ClearsExpirationOnEntity()
    {
        var entity = ActiveShortUrl(expiresAt: FutureUtc(120));
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _sut.ExecuteAsync(
            new UpdateExpirationRequest(ValidCode, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ExpiresAt.Should().BeNull();
        entity.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_OnDisabledShortUrl_AllowsUpdate()
    {
        var entity = ActiveShortUrl();
        entity.Disable();
        var newExpiry = FutureUtc();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _sut.ExecuteAsync(
            new UpdateExpirationRequest(ValidCode, newExpiry),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ExpiresAt.Should().Be(newExpiry);
    }

    [Fact]
    public async Task ExecuteAsync_OnExpiredShortUrl_AllowsUpdate()
    {
        var entity = ActiveShortUrl(expiresAt: FutureUtc(5));
        var newExpiry = FutureUtc(120);
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _sut.ExecuteAsync(
            new UpdateExpirationRequest(ValidCode, newExpiry),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ExpiresAt.Should().Be(newExpiry);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationRequested_PropagatesOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        Func<Task> act = () => _sut.ExecuteAsync(
            new UpdateExpirationRequest(ValidCode, FutureUtc()),
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
