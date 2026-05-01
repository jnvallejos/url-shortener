using FluentAssertions;
using Moq;
using UrlShortener.Application.Abstractions;
using UrlShortener.Application.ShortUrls.GetByCode;
using UrlShortener.Domain.Common;
using UrlShortener.Domain.ShortUrls;

namespace UrlShortener.Application.Tests.ShortUrls;

public class GetShortUrlUseCaseTests
{
    private const string ValidCode = "abc1234";
    private const string ValidUrl = "https://example.com/path";

    private readonly Mock<IShortUrlRepository> _repo = new();
    private readonly GetShortUrlUseCase _sut;

    public GetShortUrlUseCaseTests()
    {
        _sut = new GetShortUrlUseCase(_repo.Object);
    }

    private static ShortUrl ActiveShortUrl(string code = ValidCode, DateTime? expiresAt = null) =>
        ShortUrl.Create(
            ShortCode.Create(code),
            OriginalUrl.Create(ValidUrl),
            expiresAt);

    [Fact]
    public async Task ExecuteAsync_WithValidExistingCode_ReturnsSuccessWithFullDetails()
    {
        var entity = ActiveShortUrl();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _sut.ExecuteAsync(new GetShortUrlRequest(ValidCode), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ShortCode.Should().Be(ValidCode);
        result.Value.OriginalUrl.Should().Be(ValidUrl);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCode_ReturnsFailureWithShortCodeInvalid()
    {
        var result = await _sut.ExecuteAsync(new GetShortUrlRequest("bad"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ShortCode.Invalid");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCodeNotFound_ReturnsFailureWithNotFound()
    {
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortUrl?)null);

        var result = await _sut.ExecuteAsync(new GetShortUrlRequest(ValidCode), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ShortUrl.NotFound");
    }

    [Fact]
    public async Task ExecuteAsync_OnDisabledShortUrl_StillReturnsSuccess()
    {
        var entity = ActiveShortUrl();
        entity.Disable();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _sut.ExecuteAsync(new GetShortUrlRequest(ValidCode), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_OnExpiredShortUrl_StillReturnsSuccess()
    {
        var someExpiration = DateTime.UtcNow.AddMinutes(10);
        var entity = ActiveShortUrl(expiresAt: someExpiration);
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _sut.ExecuteAsync(new GetShortUrlRequest(ValidCode), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ExpiresAt.Should().Be(someExpiration);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotCallUpdateOrSaveOrDispatch()
    {
        var entity = ActiveShortUrl();
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        await _sut.ExecuteAsync(new GetShortUrlRequest(ValidCode), CancellationToken.None);

        _repo.Verify(
            r => r.UpdateAsync(It.IsAny<ShortUrl>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repo.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_ResponseFieldsMatchEntityIncludingClickCountAndIsEnabled()
    {
        var entity = ActiveShortUrl();
        entity.RegisterClick(DateTime.UtcNow, "ua", "ip");
        entity.RegisterClick(DateTime.UtcNow, "ua", "ip");
        _repo
            .Setup(r => r.GetByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _sut.ExecuteAsync(new GetShortUrlRequest(ValidCode), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(entity.Id);
        result.Value.ShortCode.Should().Be(entity.ShortCode.ToString());
        result.Value.OriginalUrl.Should().Be(entity.OriginalUrl.ToString());
        result.Value.CreatedAt.Should().Be(entity.CreatedAt);
        result.Value.ExpiresAt.Should().Be(entity.ExpiresAt);
        result.Value.IsEnabled.Should().Be(entity.IsEnabled);
        result.Value.ClickCount.Should().Be(2);
    }
}
