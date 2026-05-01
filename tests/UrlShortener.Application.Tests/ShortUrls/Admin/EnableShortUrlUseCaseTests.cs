using FluentAssertions;
using Moq;
using UrlShortener.Application.Abstractions;
using UrlShortener.Application.ShortUrls.Admin.Enable;
using UrlShortener.Domain.Common;
using UrlShortener.Domain.ShortUrls;

namespace UrlShortener.Application.Tests.ShortUrls.Admin;

public class EnableShortUrlUseCaseTests
{
    private const string ValidCode = "abc1234";
    private const string ValidUrl = "https://example.com/path";

    private readonly Mock<IShortUrlRepository> _repo = new();
    private readonly Mock<IDomainEventDispatcher> _dispatcher = new();
    private readonly List<string> _callLog = new();
    private readonly EnableShortUrlUseCase _sut;

    public EnableShortUrlUseCaseTests()
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

        _sut = new EnableShortUrlUseCase(_repo.Object, _dispatcher.Object);
    }

    private static ShortUrl DisabledShortUrl(string code = ValidCode)
    {
        var entity = ShortUrl.Create(
            ShortCode.Create(code),
            OriginalUrl.Create(ValidUrl));
        entity.Disable();
        return entity;
    }

    [Fact]
    public async Task ExecuteAsync_WithValidExistingCode_ReturnsSuccess()
    {
        var entity = DisabledShortUrl();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _sut.ExecuteAsync(new EnableShortUrlRequest(ValidCode), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be(ValidCode);
        result.Value.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCode_ReturnsFailureWithShortCodeInvalid()
    {
        var result = await _sut.ExecuteAsync(new EnableShortUrlRequest("bad"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ShortCode.Invalid");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCodeNotFound_ReturnsFailureWithNotFound()
    {
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortUrl?)null);

        var result = await _sut.ExecuteAsync(new EnableShortUrlRequest(ValidCode), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ShortUrl.NotFound");
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_CallsUpdateAsyncAndSaveChangesAsyncInOrder()
    {
        var entity = DisabledShortUrl();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        await _sut.ExecuteAsync(new EnableShortUrlRequest(ValidCode), CancellationToken.None);

        _callLog.Should().ContainInConsecutiveOrder("Update", "Save");
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_DispatchesEventsAfterSave()
    {
        var entity = DisabledShortUrl();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        await _sut.ExecuteAsync(new EnableShortUrlRequest(ValidCode), CancellationToken.None);

        _callLog.Should().ContainInConsecutiveOrder("Save", "Dispatch");
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_ClearsDomainEventsAfterDispatch()
    {
        var entity = DisabledShortUrl();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        await _sut.ExecuteAsync(new EnableShortUrlRequest(ValidCode), CancellationToken.None);

        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_ResponseFieldsMatchEntity()
    {
        var entity = DisabledShortUrl();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _sut.ExecuteAsync(new EnableShortUrlRequest(ValidCode), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be(entity.ShortCode.ToString());
        result.Value.IsEnabled.Should().Be(entity.IsEnabled);
    }

    [Fact]
    public async Task ExecuteAsync_OnAlreadyEnabledShortUrl_StillSucceedsAndReturnsCurrentState()
    {
        var entity = ShortUrl.Create(
            ShortCode.Create(ValidCode),
            OriginalUrl.Create(ValidUrl));
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _sut.ExecuteAsync(new EnableShortUrlRequest(ValidCode), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationRequested_PropagatesOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        Func<Task> act = () => _sut.ExecuteAsync(new EnableShortUrlRequest(ValidCode), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
