using UrlShortener.Application.Abstractions;
using UrlShortener.Application.Common;
using UrlShortener.Domain.Exceptions;
using UrlShortener.Domain.ShortUrls;

namespace UrlShortener.Application.ShortUrls.Redirect;

public sealed class RedirectUseCase
{
    private readonly IShortUrlRepository _repo;
    private readonly IDomainEventDispatcher _dispatcher;
    private readonly IDateTimeProvider _clock;

    public RedirectUseCase(
        IShortUrlRepository repo,
        IDomainEventDispatcher dispatcher,
        IDateTimeProvider clock)
    {
        _repo = repo;
        _dispatcher = dispatcher;
        _clock = clock;
    }

    public async Task<Result<RedirectResponse>> ExecuteAsync(
        RedirectRequest request,
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

        try
        {
            shortUrl.RegisterClick(_clock.UtcNow, request.UserAgent, request.IpAddress);
        }
        catch (ShortUrlExpiredException)
        {
            return Errors.ShortUrl.Expired;
        }
        catch (DomainException)
        {
            return Errors.ShortUrl.Disabled;
        }

        await _repo.UpdateAsync(shortUrl, ct);
        await _repo.SaveChangesAsync(ct);
        await _dispatcher.DispatchAsync(shortUrl.DomainEvents, ct);
        shortUrl.ClearDomainEvents();

        return new RedirectResponse(shortUrl.OriginalUrl.ToString());
    }
}
