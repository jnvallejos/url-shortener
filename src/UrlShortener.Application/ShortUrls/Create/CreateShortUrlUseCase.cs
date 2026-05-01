using UrlShortener.Application.Abstractions;
using UrlShortener.Application.Common;
using UrlShortener.Domain.ShortUrls;

namespace UrlShortener.Application.ShortUrls.Create;

public sealed class CreateShortUrlUseCase
{
    private readonly IShortUrlRepository _repo;
    private readonly IShortCodeGenerator _generator;
    private readonly IDomainEventDispatcher _dispatcher;

    public CreateShortUrlUseCase(
        IShortUrlRepository repo,
        IShortCodeGenerator generator,
        IDomainEventDispatcher dispatcher)
    {
        _repo = repo;
        _generator = generator;
        _dispatcher = dispatcher;
    }

    public async Task<Result<CreateShortUrlResponse>> ExecuteAsync(
        CreateShortUrlRequest request,
        CancellationToken ct)
    {
        var originalUrl = OriginalUrl.Create(request.OriginalUrl);

        var shortCode = ShortCode.Create(request.CustomCode!);

        var shortUrl = ShortUrl.Create(shortCode, originalUrl, request.ExpiresAt);

        await _repo.AddAsync(shortUrl, ct);
        await _repo.SaveChangesAsync(ct);
        await _dispatcher.DispatchAsync(shortUrl.DomainEvents, ct);
        shortUrl.ClearDomainEvents();

        return new CreateShortUrlResponse(
            Id:          shortUrl.Id,
            ShortCode:   shortUrl.ShortCode.ToString(),
            OriginalUrl: shortUrl.OriginalUrl.ToString(),
            ExpiresAt:   shortUrl.ExpiresAt,
            CreatedAt:   shortUrl.CreatedAt);
    }
}
