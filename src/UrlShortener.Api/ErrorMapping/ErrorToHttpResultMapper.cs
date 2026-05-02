using Microsoft.AspNetCore.Http;
using UrlShortener.Api.Contracts;
using UrlShortener.Application.Common;

namespace UrlShortener.Api.ErrorMapping;

public static class ErrorToHttpResultMapper
{
    public static IResult ToHttpResult(Error error, string? traceId = null)
    {
        var response = new ErrorResponse(error.Code, error.Message, traceId);

        return error.Code switch
        {
            "OriginalUrl.Invalid"           => Results.BadRequest(response),
            "OriginalUrl.Required"          => Results.BadRequest(response),
            "ShortCode.Invalid"             => Results.BadRequest(response),
            "Validation.InvalidExpiration"  => Results.BadRequest(response),
            "ShortUrl.NotFound"             => Results.NotFound(response),
            "ShortUrl.Disabled"             => Results.Json(response, statusCode: StatusCodes.Status410Gone),
            "ShortUrl.Expired"              => Results.Json(response, statusCode: StatusCodes.Status410Gone),
            "ShortUrl.CodeAlreadyExists"    => Results.Conflict(response),
            "ShortUrl.CodeGenerationFailed" => Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable),
            _                               => Results.Json(response, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
