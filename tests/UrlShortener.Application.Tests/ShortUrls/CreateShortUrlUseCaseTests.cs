using FluentAssertions;
using Moq;
using UrlShortener.Application.Abstractions;
using UrlShortener.Application.Common;
using UrlShortener.Application.ShortUrls.Create;
using UrlShortener.Domain.Common;
using UrlShortener.Domain.ShortUrls;

namespace UrlShortener.Application.Tests.ShortUrls;

public class CreateShortUrlUseCaseTests
{
    private const string ValidUrl = "https://example.com/path";
    private const string ValidCustomCode = "abc1234";

    private readonly Mock<IShortUrlRepository> _repo = new();
    private readonly Mock<IShortCodeGenerator> _generator = new();
    private readonly Mock<IDomainEventDispatcher> _dispatcher = new();
    private readonly List<string> _callLog = new();
    private readonly CreateShortUrlUseCase _sut;

    public CreateShortUrlUseCaseTests()
    {
        _repo
            .Setup(r => r.ExistsByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repo
            .Setup(r => r.AddAsync(It.IsAny<ShortUrl>(), It.IsAny<CancellationToken>()))
            .Callback(() => _callLog.Add("Add"))
            .Returns(Task.CompletedTask);

        _repo
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _callLog.Add("Save"))
            .Returns(Task.CompletedTask);

        _dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback(() => _callLog.Add("Dispatch"))
            .Returns(Task.CompletedTask);

        _sut = new CreateShortUrlUseCase(_repo.Object, _generator.Object, _dispatcher.Object);
    }

    private static CreateShortUrlRequest CustomCodeRequest(
        string url = ValidUrl,
        string customCode = ValidCustomCode,
        DateTime? expiresAt = null) =>
        new(url, expiresAt, customCode);

    [Fact]
    public async Task ExecuteAsync_WithValidUrlAndCustomCode_ReturnsSuccessWithCustomCode()
    {
        var result = await _sut.ExecuteAsync(CustomCodeRequest(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ShortCode.Should().Be(ValidCustomCode);
        result.Value.OriginalUrl.Should().Be(ValidUrl);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidUrlAndExpiration_ReturnsSuccessWithExpiration()
    {
        var expiresAt = DateTime.UtcNow.AddHours(1);

        var result = await _sut.ExecuteAsync(CustomCodeRequest(expiresAt: expiresAt), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomCode_DoesNotCallCodeGenerator()
    {
        await _sut.ExecuteAsync(CustomCodeRequest(), CancellationToken.None);

        _generator.Verify(
            g => g.GenerateAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_CallsAddAsyncAndSaveChangesAsyncInOrder()
    {
        await _sut.ExecuteAsync(CustomCodeRequest(), CancellationToken.None);

        _callLog.Should().ContainInConsecutiveOrder("Add", "Save");
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_CallsDispatcherWithEmptyEventsAfterSave()
    {
        IEnumerable<IDomainEvent>? capturedEvents = null;
        _dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<IDomainEvent>, CancellationToken>((events, _) =>
            {
                _callLog.Add("Dispatch");
                capturedEvents = events.ToList();
            })
            .Returns(Task.CompletedTask);

        await _sut.ExecuteAsync(CustomCodeRequest(), CancellationToken.None);

        capturedEvents.Should().NotBeNull().And.BeEmpty();
        _callLog.Should().ContainInConsecutiveOrder("Save", "Dispatch");
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_ClearsDomainEventsAfterDispatch()
    {
        ShortUrl? added = null;
        _repo
            .Setup(r => r.AddAsync(It.IsAny<ShortUrl>(), It.IsAny<CancellationToken>()))
            .Callback<ShortUrl, CancellationToken>((s, _) =>
            {
                _callLog.Add("Add");
                added = s;
            })
            .Returns(Task.CompletedTask);

        await _sut.ExecuteAsync(CustomCodeRequest(), CancellationToken.None);

        added.Should().NotBeNull();
        added!.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_ResponseFieldsMatchEntity()
    {
        ShortUrl? added = null;
        _repo
            .Setup(r => r.AddAsync(It.IsAny<ShortUrl>(), It.IsAny<CancellationToken>()))
            .Callback<ShortUrl, CancellationToken>((s, _) =>
            {
                _callLog.Add("Add");
                added = s;
            })
            .Returns(Task.CompletedTask);

        var result = await _sut.ExecuteAsync(CustomCodeRequest(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        added.Should().NotBeNull();
        result.Value.Id.Should().Be(added!.Id);
        result.Value.ShortCode.Should().Be(added.ShortCode.ToString());
        result.Value.OriginalUrl.Should().Be(added.OriginalUrl.ToString());
        result.Value.CreatedAt.Should().Be(added.CreatedAt);
        result.Value.ExpiresAt.Should().Be(added.ExpiresAt);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidOriginalUrl_ReturnsFailureWithOriginalUrlInvalid()
    {
        var request = new CreateShortUrlRequest("not-a-url", null, ValidCustomCode);

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OriginalUrl.Invalid");
        result.Error.Message.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidOriginalUrl_DoesNotCallRepositoryOrGenerator()
    {
        var request = new CreateShortUrlRequest("not-a-url", null, ValidCustomCode);

        await _sut.ExecuteAsync(request, CancellationToken.None);

        _repo.Verify(
            r => r.ExistsByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repo.Verify(
            r => r.AddAsync(It.IsAny<ShortUrl>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repo.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        _generator.Verify(
            g => g.GenerateAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCustomCode_ReturnsFailureWithShortCodeInvalid()
    {
        var request = CustomCodeRequest(customCode: "abc");

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ShortCode.Invalid");
        result.Error.Message.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCustomCode_DoesNotCallRepository()
    {
        var request = CustomCodeRequest(customCode: "abc");

        await _sut.ExecuteAsync(request, CancellationToken.None);

        _repo.Verify(
            r => r.ExistsByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repo.Verify(
            r => r.AddAsync(It.IsAny<ShortUrl>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repo.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithDuplicateCustomCode_ReturnsFailureWithCodeAlreadyExists()
    {
        _repo
            .Setup(r => r.ExistsByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.ExecuteAsync(CustomCodeRequest(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ShortUrl.CodeAlreadyExists");
        result.Error.Message.Should().Contain(ValidCustomCode);
    }

    [Fact]
    public async Task ExecuteAsync_WithDuplicateCustomCode_DoesNotCallAddOrSave()
    {
        _repo
            .Setup(r => r.ExistsByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.ExecuteAsync(CustomCodeRequest(), CancellationToken.None);

        _repo.Verify(
            r => r.AddAsync(It.IsAny<ShortUrl>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repo.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithPastExpiration_ReturnsFailureWithInvalidExpiration()
    {
        var pastExpiration = DateTime.UtcNow.AddHours(-1);

        var result = await _sut.ExecuteAsync(
            CustomCodeRequest(expiresAt: pastExpiration),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.InvalidExpiration");
        result.Error.Message.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithPastExpiration_DoesNotCallAddOrSave()
    {
        var pastExpiration = DateTime.UtcNow.AddHours(-1);

        await _sut.ExecuteAsync(
            CustomCodeRequest(expiresAt: pastExpiration),
            CancellationToken.None);

        _repo.Verify(
            r => r.AddAsync(It.IsAny<ShortUrl>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repo.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static CreateShortUrlRequest GeneratedCodeRequest(
        string url = ValidUrl,
        DateTime? expiresAt = null) =>
        new(url, expiresAt, null);

    private static ShortCode FreshCode(string value) => ShortCode.Create(value);

    [Fact]
    public async Task ExecuteAsync_WithValidUrlAndNoCustomCode_ReturnsSuccessWithGeneratedCode()
    {
        const string generated = "gen0001";
        _generator
            .Setup(g => g.GenerateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(FreshCode(generated));

        var result = await _sut.ExecuteAsync(GeneratedCodeRequest(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ShortCode.Should().Be(generated);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutCustomCode_CallsCodeGenerator()
    {
        _generator
            .Setup(g => g.GenerateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(FreshCode("gen0001"));

        await _sut.ExecuteAsync(GeneratedCodeRequest(), CancellationToken.None);

        _generator.Verify(
            g => g.GenerateAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGeneratedCodeCollidesOnce_RetriesAndSucceedsOnSecondAttempt()
    {
        _generator
            .SetupSequence(g => g.GenerateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(FreshCode("first01"))
            .ReturnsAsync(FreshCode("second2"));

        _repo
            .SetupSequence(r => r.ExistsByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);

        var result = await _sut.ExecuteAsync(GeneratedCodeRequest(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ShortCode.Should().Be("second2");
        _generator.Verify(
            g => g.GenerateAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllAttemptsCollide_ReturnsFailureWithCodeGenerationFailed()
    {
        _generator
            .Setup(g => g.GenerateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => FreshCode("collide"));

        _repo
            .Setup(r => r.ExistsByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.ExecuteAsync(GeneratedCodeRequest(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ShortUrl.CodeGenerationFailed");
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllAttemptsCollide_DoesNotCallAddOrSave()
    {
        _generator
            .Setup(g => g.GenerateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => FreshCode("collide"));

        _repo
            .Setup(r => r.ExistsByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.ExecuteAsync(GeneratedCodeRequest(), CancellationToken.None);

        _repo.Verify(
            r => r.AddAsync(It.IsAny<ShortUrl>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repo.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGeneratorRetries_DoesNotExceedMaxAttempts()
    {
        const int expectedMaxAttempts = 5;

        _generator
            .Setup(g => g.GenerateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => FreshCode("collide"));

        _repo
            .Setup(r => r.ExistsByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.ExecuteAsync(GeneratedCodeRequest(), CancellationToken.None);

        _generator.Verify(
            g => g.GenerateAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(expectedMaxAttempts));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationRequested_PropagatesOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _repo
            .Setup(r => r.ExistsByCodeAsync(It.IsAny<ShortCode>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        Func<Task> act = () => _sut.ExecuteAsync(CustomCodeRequest(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
