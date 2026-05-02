using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using UrlShortener.Api.Contracts;
using UrlShortener.Api.ErrorMapping;
using UrlShortener.Application.Common;

namespace UrlShortener.Api.Tests.ErrorMapping;

public class ErrorToHttpResultMapperTests
{
    [Fact]
    public void ToHttpResult_OnOriginalUrlInvalid_Returns400()
    {
        var result = ErrorToHttpResultMapper.ToHttpResult(Errors.OriginalUrl.Invalid("bad url"));

        StatusCodeFor(result).Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void ToHttpResult_OnOriginalUrlRequired_Returns400()
    {
        var error = new Error("OriginalUrl.Required", "Original URL is required");

        var result = ErrorToHttpResultMapper.ToHttpResult(error);

        StatusCodeFor(result).Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void ToHttpResult_OnShortCodeInvalid_Returns400()
    {
        var result = ErrorToHttpResultMapper.ToHttpResult(Errors.ShortCode.Invalid("bad code"));

        StatusCodeFor(result).Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void ToHttpResult_OnInvalidExpiration_Returns400()
    {
        var result = ErrorToHttpResultMapper.ToHttpResult(Errors.Validation.InvalidExpiration("in the past"));

        StatusCodeFor(result).Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void ToHttpResult_OnShortUrlNotFound_Returns404()
    {
        var result = ErrorToHttpResultMapper.ToHttpResult(Errors.ShortUrl.NotFound);

        StatusCodeFor(result).Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void ToHttpResult_OnShortUrlDisabled_Returns410()
    {
        var result = ErrorToHttpResultMapper.ToHttpResult(Errors.ShortUrl.Disabled);

        StatusCodeFor(result).Should().Be(StatusCodes.Status410Gone);
    }

    [Fact]
    public void ToHttpResult_OnShortUrlExpired_Returns410()
    {
        var result = ErrorToHttpResultMapper.ToHttpResult(Errors.ShortUrl.Expired);

        StatusCodeFor(result).Should().Be(StatusCodes.Status410Gone);
    }

    [Fact]
    public void ToHttpResult_OnCodeAlreadyExists_Returns409()
    {
        var result = ErrorToHttpResultMapper.ToHttpResult(Errors.ShortUrl.CodeAlreadyExists("abc1234"));

        StatusCodeFor(result).Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void ToHttpResult_OnCodeGenerationFailed_Returns503()
    {
        var result = ErrorToHttpResultMapper.ToHttpResult(Errors.ShortUrl.CodeGenerationFailed);

        StatusCodeFor(result).Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public void ToHttpResult_OnUnknownErrorCode_Returns500()
    {
        var error = new Error("Unmapped.Code", "something unmapped");

        var result = ErrorToHttpResultMapper.ToHttpResult(error);

        StatusCodeFor(result).Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public void ToHttpResult_IncludesTraceIdInResponseBody()
    {
        var traceId = "trace-abc-123";

        var result = ErrorToHttpResultMapper.ToHttpResult(Errors.ShortUrl.NotFound, traceId);

        var body = ResponseBody(result);
        body.Should().NotBeNull();
        body!.TraceId.Should().Be(traceId);
    }

    [Fact]
    public void ToHttpResult_IncludesErrorCodeInResponseBody()
    {
        var error = Errors.ShortUrl.CodeAlreadyExists("abc1234");

        var result = ErrorToHttpResultMapper.ToHttpResult(error);

        var body = ResponseBody(result);
        body.Should().NotBeNull();
        body!.Code.Should().Be("ShortUrl.CodeAlreadyExists");
        body.Message.Should().Contain("abc1234");
    }

    private static int? StatusCodeFor(IResult result) =>
        (result as IStatusCodeHttpResult)?.StatusCode;

    private static ErrorResponse? ResponseBody(IResult result) => result switch
    {
        IValueHttpResult<ErrorResponse> typed => typed.Value,
        IValueHttpResult valueResult          => valueResult.Value as ErrorResponse,
        _                                     => null
    };
}
