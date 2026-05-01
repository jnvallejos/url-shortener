using UrlShortener.Application.Abstractions;
using UrlShortener.Application.Common;
using UrlShortener.Domain.Exceptions;
using UrlShortener.Domain.ShortUrls;

namespace UrlShortener.Application.ShortUrls.Admin.Enable;

public sealed class EnableShortUrlUseCase
{
    private readonly IShortUrlRepository _repo;
    private readonly IDomainEventDispatcher _dispatcher;

    public EnableShortUrlUseCase(
        IShortUrlRepository repo,
        IDomainEventDispatcher dispatcher)
    {
        _repo = repo;
        _dispatcher = dispatcher;
    }

    public async Task<Result<EnableShortUrlResponse>> ExecuteAsync(
        EnableShortUrlRequest request,
        CancellationToken ct)
    {
        ShortCode shortCode;
        try
        {
            shortCode = ShortCode.Create(request.Code);
        }
        catch (InvalidShortCodeException ex)
        {
            return Errors.ShortCode.Invalid(ex.Message);
        }

        var shortUrl = await _repo.GetByCodeAsync(shortCode, ct);
        if (shortUrl is null)
        {
            return Errors.ShortUrl.NotFound;
        }

        shortUrl.Enable();

        await _repo.UpdateAsync(shortUrl, ct);
        await _repo.SaveChangesAsync(ct);
        await _dispatcher.DispatchAsync(shortUrl.DomainEvents, ct);
        shortUrl.ClearDomainEvents();

        return new EnableShortUrlResponse(shortUrl.ShortCode.ToString(), shortUrl.IsEnabled);
    }
}
