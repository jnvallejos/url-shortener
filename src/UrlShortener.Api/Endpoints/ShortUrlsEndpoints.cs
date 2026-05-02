using UrlShortener.Api.Contracts;
using UrlShortener.Api.ErrorMapping;
using UrlShortener.Application.Common;
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
        var group = endpoints.MapGroup("/api/shorturls").WithTags("ShortUrls");

        group.MapPost("/", CreateShortUrlAsync)
            .WithName("CreateShortUrl")
            .WithSummary("Create a new shortened URL")
            .WithDescription("Generates a 7-character Base62 code (or accepts a custom code) and persists the short URL.")
            .Produces<ShortUrlContract>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/{code}", GetShortUrlAsync)
            .WithName("GetShortUrl")
            .WithSummary("Get a short URL by code")
            .WithDescription("Returns the full short URL record including state and click count. Read-only; does not register a click.")
            .Produces<ShortUrlContract>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/{code}/disable", DisableShortUrlAsync)
            .WithName("DisableShortUrl")
            .WithSummary("Disable a short URL")
            .WithDescription("Marks the short URL as disabled. Idempotent.")
            .Produces<ShortUrlStateContract>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/{code}/enable", EnableShortUrlAsync)
            .WithName("EnableShortUrl")
            .WithSummary("Enable a short URL")
            .WithDescription("Marks the short URL as enabled. Idempotent.")
            .Produces<ShortUrlStateContract>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPatch("/{code}/expiration", UpdateExpirationAsync)
            .WithName("UpdateExpiration")
            .WithSummary("Update or clear a short URL's expiration")
            .WithDescription("Sets a new expiration timestamp (must be in the future) or clears it when null is provided.")
            .Produces<ShortUrlExpirationContract>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> CreateShortUrlAsync(
        CreateShortUrlContract? request,
        HttpContext httpContext,
        CreateShortUrlUseCase useCase,
        ILogger<ShortUrlsEndpointsLog> logger,
        CancellationToken ct)
    {
        logger.LogInformation("Creating short URL for original {OriginalUrl}", request?.OriginalUrl);

        if (request is null)
        {
            logger.LogInformation("CreateShortUrl rejected: missing request body");
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
            LogFailure(logger, "CreateShortUrl", result.Error);
            return ErrorToHttpResultMapper.ToHttpResult(result.Error, httpContext.TraceIdentifier);
        }

        var response = result.Value;
        logger.LogInformation("Created short URL {ShortCode} for {OriginalUrl}", response.ShortCode, response.OriginalUrl);

        var contract = new ShortUrlContract(
            Id:          response.Id,
            ShortCode:   response.ShortCode,
            OriginalUrl: response.OriginalUrl,
            ShortUrl:    BuildShortUrl(httpContext, response.ShortCode),
            ExpiresAt:   response.ExpiresAt,
            CreatedAt:   response.CreatedAt,
            IsEnabled:   true,
            ClickCount:  0);

        return Results.Created($"/api/shorturls/{response.ShortCode}", contract);
    }

    private static async Task<IResult> GetShortUrlAsync(
        string code,
        HttpContext httpContext,
        GetShortUrlUseCase useCase,
        ILogger<ShortUrlsEndpointsLog> logger,
        CancellationToken ct)
    {
        logger.LogInformation("Fetching short URL {ShortCode}", code);

        var result = await useCase.ExecuteAsync(new GetShortUrlRequest(code), ct);
        if (result.IsFailure)
        {
            LogFailure(logger, "GetShortUrl", result.Error);
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

    private static async Task<IResult> DisableShortUrlAsync(
        string code,
        HttpContext httpContext,
        DisableShortUrlUseCase useCase,
        ILogger<ShortUrlsEndpointsLog> logger,
        CancellationToken ct)
    {
        logger.LogInformation("Disabling short URL {ShortCode}", code);

        var result = await useCase.ExecuteAsync(new DisableShortUrlRequest(code), ct);
        if (result.IsFailure)
        {
            LogFailure(logger, "DisableShortUrl", result.Error);
            return ErrorToHttpResultMapper.ToHttpResult(result.Error, httpContext.TraceIdentifier);
        }

        logger.LogInformation("Disabled short URL {ShortCode}", result.Value.Code);
        return Results.Ok(new ShortUrlStateContract(result.Value.Code, result.Value.IsEnabled));
    }

    private static async Task<IResult> EnableShortUrlAsync(
        string code,
        HttpContext httpContext,
        EnableShortUrlUseCase useCase,
        ILogger<ShortUrlsEndpointsLog> logger,
        CancellationToken ct)
    {
        logger.LogInformation("Enabling short URL {ShortCode}", code);

        var result = await useCase.ExecuteAsync(new EnableShortUrlRequest(code), ct);
        if (result.IsFailure)
        {
            LogFailure(logger, "EnableShortUrl", result.Error);
            return ErrorToHttpResultMapper.ToHttpResult(result.Error, httpContext.TraceIdentifier);
        }

        logger.LogInformation("Enabled short URL {ShortCode}", result.Value.Code);
        return Results.Ok(new ShortUrlStateContract(result.Value.Code, result.Value.IsEnabled));
    }

    private static async Task<IResult> UpdateExpirationAsync(
        string code,
        UpdateExpirationContract? request,
        HttpContext httpContext,
        UpdateExpirationUseCase useCase,
        ILogger<ShortUrlsEndpointsLog> logger,
        CancellationToken ct)
    {
        logger.LogInformation("Updating expiration on short URL {ShortCode}", code);

        if (request is null)
        {
            logger.LogInformation("UpdateExpiration rejected for {ShortCode}: missing request body", code);
            return Results.BadRequest(new ErrorResponse(
                "Request.Missing",
                "Request body is required",
                httpContext.TraceIdentifier));
        }

        var useCaseRequest = new UpdateExpirationRequest(code, request.NewExpiresAt);
        var result = await useCase.ExecuteAsync(useCaseRequest, ct);
        if (result.IsFailure)
        {
            LogFailure(logger, "UpdateExpiration", result.Error);
            return ErrorToHttpResultMapper.ToHttpResult(result.Error, httpContext.TraceIdentifier);
        }

        logger.LogInformation("Updated expiration on short URL {ShortCode} to {ExpiresAt}",
            result.Value.Code, result.Value.ExpiresAt);
        return Results.Ok(new ShortUrlExpirationContract(result.Value.Code, result.Value.ExpiresAt));
    }

    private static void LogFailure(ILogger logger, string operation, Error error)
    {
        switch (error.Code)
        {
            case "ShortUrl.CodeGenerationFailed":
                logger.LogWarning("{Operation} failed: {ErrorCode} {ErrorMessage}", operation, error.Code, error.Message);
                break;
            case "OriginalUrl.Invalid":
            case "OriginalUrl.Required":
            case "ShortCode.Invalid":
            case "Validation.InvalidExpiration":
            case "ShortUrl.NotFound":
            case "ShortUrl.Disabled":
            case "ShortUrl.Expired":
            case "ShortUrl.CodeAlreadyExists":
                logger.LogInformation("{Operation} failed: {ErrorCode} {ErrorMessage}", operation, error.Code, error.Message);
                break;
            default:
                logger.LogError("{Operation} failed with unmapped error: {ErrorCode} {ErrorMessage}", operation, error.Code, error.Message);
                break;
        }
    }

    private static string BuildShortUrl(HttpContext httpContext, string shortCode) =>
        $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/{shortCode}";
}

internal sealed class ShortUrlsEndpointsLog;
