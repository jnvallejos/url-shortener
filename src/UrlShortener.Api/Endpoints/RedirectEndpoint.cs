using Microsoft.AspNetCore.RateLimiting;
using UrlShortener.Api.Contracts;
using UrlShortener.Api.ErrorMapping;
using UrlShortener.Application.Common;
using UrlShortener.Application.ShortUrls.Redirect;

namespace UrlShortener.Api.Endpoints;

public static class RedirectEndpoint
{
    public static IEndpointRouteBuilder MapRedirectEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{code:length(7):regex(^[A-Za-z0-9]+$)}", RedirectAsync)
            .WithName("Redirect")
            .WithTags("Redirect")
            .WithSummary("Redirect to the original URL")
            .WithDescription("Resolves the short code, registers a click, and issues a 302 redirect to the original URL. Rate-limited per IP.")
            .Produces(StatusCodes.Status302Found)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status410Gone)
            .Produces<ErrorResponse>(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting("redirect");

        return endpoints;
    }

    private static async Task<IResult> RedirectAsync(
        string code,
        HttpContext httpContext,
        RedirectUseCase useCase,
        ILogger<RedirectEndpointLog> logger,
        CancellationToken ct)
    {
        logger.LogInformation("Resolving redirect for {ShortCode}", code);

        var request = new RedirectRequest(
            Code:      code,
            UserAgent: httpContext.Request.Headers.UserAgent.ToString(),
            IpAddress: httpContext.Connection.RemoteIpAddress?.ToString());

        var result = await useCase.ExecuteAsync(request, ct);
        if (result.IsFailure)
        {
            LogFailure(logger, result.Error);
            return ErrorToHttpResultMapper.ToHttpResult(result.Error, httpContext.TraceIdentifier);
        }

        logger.LogInformation("Redirecting {ShortCode} to {OriginalUrl}", code, result.Value.OriginalUrl);
        return Results.Redirect(result.Value.OriginalUrl, permanent: false, preserveMethod: false);
    }

    private static void LogFailure(ILogger logger, Error error)
    {
        switch (error.Code)
        {
            case "ShortUrl.NotFound":
            case "ShortUrl.Disabled":
            case "ShortUrl.Expired":
            case "ShortCode.Invalid":
                logger.LogInformation("Redirect failed: {ErrorCode} {ErrorMessage}", error.Code, error.Message);
                break;
            default:
                logger.LogError("Redirect failed with unmapped error: {ErrorCode} {ErrorMessage}", error.Code, error.Message);
                break;
        }
    }
}

internal sealed class RedirectEndpointLog;
