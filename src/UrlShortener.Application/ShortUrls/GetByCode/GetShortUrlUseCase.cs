using UrlShortener.Application.Abstractions;
using UrlShortener.Application.Common;
using UrlShortener.Domain.Exceptions;
using UrlShortener.Domain.ShortUrls;

namespace UrlShortener.Application.ShortUrls.GetByCode;

public sealed class GetShortUrlUseCase
{
    private readonly IShortUrlRepository _repo;

    public GetShortUrlUseCase(IShortUrlRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<GetShortUrlResponse>> ExecuteAsync(
        GetShortUrlRequest request,
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

        return new GetShortUrlResponse(
            Id:          shortUrl.Id,
            ShortCode:   shortUrl.ShortCode.ToString(),
            OriginalUrl: shortUrl.OriginalUrl.ToString(),
            ExpiresAt:   shortUrl.ExpiresAt,
            CreatedAt:   shortUrl.CreatedAt,
            IsEnabled:   shortUrl.IsEnabled,
            ClickCount:  shortUrl.ClickCount);
    }
}
