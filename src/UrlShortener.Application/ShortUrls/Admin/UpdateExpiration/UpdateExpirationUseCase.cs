using UrlShortener.Application.Abstractions;
using UrlShortener.Application.Common;
using UrlShortener.Domain.Exceptions;
using UrlShortener.Domain.ShortUrls;

namespace UrlShortener.Application.ShortUrls.Admin.UpdateExpiration;

public sealed class UpdateExpirationUseCase
{
    private readonly IShortUrlRepository _repo;
    private readonly IDomainEventDispatcher _dispatcher;

    public UpdateExpirationUseCase(
        IShortUrlRepository repo,
        IDomainEventDispatcher dispatcher)
    {
        _repo = repo;
        _dispatcher = dispatcher;
    }

    public async Task<Result<UpdateExpirationResponse>> ExecuteAsync(
        UpdateExpirationRequest request,
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
            shortUrl.UpdateExpiration(request.NewExpiresAt);
        }
        catch (DomainException ex)
        {
            return Errors.Validation.InvalidExpiration(ex.Message);
        }

        await _repo.UpdateAsync(shortUrl, ct);
        await _repo.SaveChangesAsync(ct);
        await _dispatcher.DispatchAsync(shortUrl.DomainEvents, ct);
        shortUrl.ClearDomainEvents();

        return new UpdateExpirationResponse(shortUrl.ShortCode.ToString(), shortUrl.ExpiresAt);
    }
}
