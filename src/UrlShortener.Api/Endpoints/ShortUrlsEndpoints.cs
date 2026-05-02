using UrlShortener.Api.Contracts;
using UrlShortener.Api.ErrorMapping;
using UrlShortener.Application.ShortUrls.Admin.Disable;
using UrlShortener.Application.ShortUrls.Admin.Enable;
using UrlShortener.Application.ShortUrls.Admin.UpdateExpiration;
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

        group.MapPost("/{code}/disable", DisableShortUrlAsync)
            .WithName("DisableShortUrl");

        group.MapPost("/{code}/enable", EnableShortUrlAsync)
            .WithName("EnableShortUrl");

        group.MapPatch("/{code}/expiration", UpdateExpirationAsync)
            .WithName("UpdateExpiration");

        return endpoints;
    }

    private static async Task<IResult> UpdateExpirationAsync(
        string code,
        UpdateExpirationContract? request,
        HttpContext httpContext,
        UpdateExpirationUseCase useCase,
        CancellationToken ct)
    {
        if (request is null)
        {
            return Results.BadRequest(new ErrorResponse(
                "Request.Missing",
                "Request body is required",
                httpContext.TraceIdentifier));
        }

        var useCaseRequest = new UpdateExpirationRequest(code, request.NewExpiresAt);
        var result = await useCase.ExecuteAsync(useCaseRequest, ct);
        if (result.IsFailure)
        {
            return ErrorToHttpResultMapper.ToHttpResult(result.Error, httpContext.TraceIdentifier);
        }

        return Results.Ok(new ShortUrlExpirationContract(result.Value.Code, result.Value.ExpiresAt));
    }

    private static async Task<IResult> DisableShortUrlAsync(
        string code,
        HttpContext httpContext,
        DisableShortUrlUseCase useCase,
        CancellationToken ct)
    {
        var result = await useCase.ExecuteAsync(new DisableShortUrlRequest(code), ct);
        if (result.IsFailure)
        {
            return ErrorToHttpResultMapper.ToHttpResult(result.Error, httpContext.TraceIdentifier);
        }

        return Results.Ok(new ShortUrlStateContract(result.Value.Code, result.Value.IsEnabled));
    }

    private static async Task<IResult> EnableShortUrlAsync(
        string code,
        HttpContext httpContext,
        EnableShortUrlUseCase useCase,
        CancellationToken ct)
    {
        var result = await useCase.ExecuteAsync(new EnableShortUrlRequest(code), ct);
        if (result.IsFailure)
        {
            return ErrorToHttpResultMapper.ToHttpResult(result.Error, httpContext.TraceIdentifier);
        }

        return Results.Ok(new ShortUrlStateContract(result.Value.Code, result.Value.IsEnabled));
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
