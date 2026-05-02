using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using UrlShortener.Api.Contracts;
using UrlShortener.Api.Tests.TestSupport;

namespace UrlShortener.Api.Tests.RateLimiting;

public class RedirectRateLimitTests : IDisposable
{
    private readonly RateLimitedApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Get_AfterPermitLimitExceeded_Returns429()
    {
        const string code = "RateL01";
        await _factory.SeedShortUrlAsync(code);
        var client = NoRedirectClient();

        for (var i = 0; i < _factory.PermitLimit; i++)
        {
            var ok = await client.GetAsync("/" + code);
            ok.StatusCode.Should().Be(HttpStatusCode.Found);
        }

        var rejected = await client.GetAsync("/" + code);

        ((int)rejected.StatusCode).Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public async Task Get_429Response_IncludesRetryAfterHeader()
    {
        const string code = "RateL02";
        await _factory.SeedShortUrlAsync(code);
        var client = NoRedirectClient();

        for (var i = 0; i < _factory.PermitLimit; i++)
        {
            await client.GetAsync("/" + code);
        }
        var rejected = await client.GetAsync("/" + code);

        rejected.Headers.Should().ContainKey("Retry-After");
    }

    [Fact]
    public async Task Get_429Response_BodyMatchesErrorResponseShape()
    {
        const string code = "RateL03";
        await _factory.SeedShortUrlAsync(code);
        var client = NoRedirectClient();

        for (var i = 0; i < _factory.PermitLimit; i++)
        {
            await client.GetAsync("/" + code);
        }
        var rejected = await client.GetAsync("/" + code);
        var body = await rejected.Content.ReadFromJsonAsync<ErrorResponse>();

        body.Should().NotBeNull();
        body!.Code.Should().Be("RateLimit.Exceeded");
        body.Message.Should().NotBeNullOrEmpty();
    }

    [Fact(Skip = "WebApplicationFactory provides a fixed RemoteIpAddress; partition isolation by IP cannot be exercised through TestServer without bespoke middleware. Behavior is verified by manual test against a real listener.")]
    public Task Get_DifferentIpsHaveSeparatePartitions() => Task.CompletedTask;

    [Fact]
    public async Task Get_AfterWindowExpires_AcceptsNewRequests()
    {
        const string code = "RateL04";
        await _factory.SeedShortUrlAsync(code);
        var client = NoRedirectClient();

        for (var i = 0; i < _factory.PermitLimit; i++)
        {
            await client.GetAsync("/" + code);
        }
        var rejected = await client.GetAsync("/" + code);
        ((int)rejected.StatusCode).Should().Be(StatusCodes.Status429TooManyRequests);

        await Task.Delay(_factory.Window + TimeSpan.FromMilliseconds(500));

        var afterWindow = await client.GetAsync("/" + code);
        afterWindow.StatusCode.Should().Be(HttpStatusCode.Found);
    }

    private HttpClient NoRedirectClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
}
