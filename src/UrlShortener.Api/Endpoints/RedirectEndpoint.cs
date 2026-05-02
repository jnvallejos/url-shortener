using UrlShortener.Api.ErrorMapping;
using UrlShortener.Application.ShortUrls.Redirect;

namespace UrlShortener.Api.Endpoints;

public static class RedirectEndpoint
{
    public static IEndpointRouteBuilder MapRedirectEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{code:length(7):regex(^[A-Za-z0-9]+$)}", RedirectAsync)
            .WithName("Redirect");

        return endpoints;
    }

    private static async Task<IResult> RedirectAsync(
        string code,
        HttpContext httpContext,
        RedirectUseCase useCase,
        CancellationToken ct)
    {
        var request = new RedirectRequest(
            Code:      code,
            UserAgent: httpContext.Request.Headers.UserAgent.ToString(),
            IpAddress: httpContext.Connection.RemoteIpAddress?.ToString());

        var result = await useCase.ExecuteAsync(request, ct);
        if (result.IsFailure)
        {
            return ErrorToHttpResultMapper.ToHttpResult(result.Error, httpContext.TraceIdentifier);
        }

        return Results.Redirect(result.Value.OriginalUrl, permanent: false, preserveMethod: false);
    }
}
