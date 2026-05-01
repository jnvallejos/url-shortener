using UrlShortener.Application.Abstractions;
using UrlShortener.Application.Common;
using UrlShortener.Domain.Exceptions;
using UrlShortener.Domain.ShortUrls;

namespace UrlShortener.Application.ShortUrls.Create;

public sealed class CreateShortUrlUseCase
{
    private const int MaxCodeGenerationAttempts = 5;

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
        OriginalUrl originalUrl;
        try
        {
            originalUrl = OriginalUrl.Create(request.OriginalUrl);
        }
        catch (InvalidOriginalUrlException ex)
        {
            return Errors.OriginalUrl.Invalid(ex.Message);
        }

        ShortCode? shortCode;
        if (!string.IsNullOrWhiteSpace(request.CustomCode))
        {
            try
            {
                shortCode = ShortCode.Create(request.CustomCode);
            }
            catch (InvalidShortCodeException ex)
            {
                return Errors.ShortCode.Invalid(ex.Message);
            }

            if (await _repo.ExistsByCodeAsync(shortCode, ct))
            {
                return Errors.ShortUrl.CodeAlreadyExists(shortCode.ToString());
            }
        }
        else
        {
            shortCode = null;
            for (var attempt = 0; attempt < MaxCodeGenerationAttempts; attempt++)
            {
                var candidate = await _generator.GenerateAsync(ct);
                if (!await _repo.ExistsByCodeAsync(candidate, ct))
                {
                    shortCode = candidate;
                    break;
                }
            }

            if (shortCode is null)
            {
                return Errors.ShortUrl.CodeGenerationFailed;
            }
        }

        ShortUrl shortUrl;
        try
        {
            shortUrl = ShortUrl.Create(shortCode, originalUrl, request.ExpiresAt);
        }
        catch (DomainException ex)
        {
            return Errors.Validation.InvalidExpiration(ex.Message);
        }

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
