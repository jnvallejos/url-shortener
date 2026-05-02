using UrlShortener.Api.Contracts;
using UrlShortener.Api.ErrorMapping;
using UrlShortener.Application.ShortUrls.Create;
using UrlShortener.Application.ShortUrls.GetByCode;

namespace UrlShortener.Api.Endpoints;

public static class ShortUrlsEndpoints
{
    public static IEndpointRouteBuilder MapShortUrlsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/shorturls");

        group.MapPost("/", CreateShortUrlAsync)
            .WithName("CreateShortUrl");

        group.MapGet("/{code}", GetShortUrlAsync)
            .WithName("GetShortUrl");

        return endpoints;
    }

    private static async Task<IResult> GetShortUrlAsync(
        string code,
        HttpContext httpContext,
        GetShortUrlUseCase useCase,
        CancellationToken ct)
    {
        var result = await useCase.ExecuteAsync(new GetShortUrlRequest(code), ct);
        if (result.IsFailure)
        {
            return ErrorToHttpResultMapper.ToHttpResult(result.Error, httpContext.TraceIdentifier);
        }

        var response = result.Value;
        var contract = new ShortUrlContract(
            Id:          response.Id,
            ShortCode:   response.ShortCode,
            OriginalUrl: response.OriginalUrl,
            ShortUrl:    BuildShortUrl(httpContext, response.ShortCode),
            ExpiresAt:   response.ExpiresAt,
            CreatedAt:   response.CreatedAt,
            IsEnabled:   response.IsEnabled,
            ClickCount:  response.ClickCount);

        return Results.Ok(contract);
    }

    private static async Task<IResult> CreateShortUrlAsync(
        CreateShortUrlContract? request,
        HttpContext httpContext,
        CreateShortUrlUseCase useCase,
        CancellationToken ct)
    {
        if (request is null)
        {
            return Results.BadRequest(new ErrorResponse(
                "Request.Missing",
                "Request body is required",
                httpContext.TraceIdentifier));
        }

        var useCaseRequest = new CreateShortUrlRequest(
            OriginalUrl: request.OriginalUrl,
            ExpiresAt:   request.ExpiresAt,
            CustomCode:  request.CustomCode);

        var result = await useCase.ExecuteAsync(useCaseRequest, ct);
        if (result.IsFailure)
        {
            return ErrorToHttpResultMapper.ToHttpResult(result.Error, httpContext.TraceIdentifier);
        }

        var response = result.Value;
        var shortUrl = BuildShortUrl(httpContext, response.ShortCode);

        var contract = new ShortUrlContract(
            Id:          response.Id,
            ShortCode:   response.ShortCode,
            OriginalUrl: response.OriginalUrl,
            ShortUrl:    shortUrl,
            ExpiresAt:   response.ExpiresAt,
            CreatedAt:   response.CreatedAt,
            IsEnabled:   true,
            ClickCount:  0);

        return Results.Created($"/api/shorturls/{response.ShortCode}", contract);
    }

    private static string BuildShortUrl(HttpContext httpContext, string shortCode) =>
        $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/{shortCode}";
}
