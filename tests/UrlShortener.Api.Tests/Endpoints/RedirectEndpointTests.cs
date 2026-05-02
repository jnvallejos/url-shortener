using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Api.Contracts;
using UrlShortener.Api.Tests.TestSupport;

namespace UrlShortener.Api.Tests.Endpoints;

public class RedirectEndpointTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public RedirectEndpointTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Clock.Reset();
    }

    [Fact]
    public async Task Get_WithValidExistingCode_Returns302WithLocationHeader()
    {
        const string code = "Redir01";
        await _factory.SeedShortUrlAsync(code, originalUrl: "https://example.com/r1");
        var client = NoRedirectClient();

        var response = await client.GetAsync("/" + code);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("https://example.com/r1");
    }

    [Fact]
    public async Task Get_WithValidExistingCode_IncrementsClickCountInDatabase()
    {
        const string code = "Redir02";
        await _factory.SeedShortUrlAsync(code);
        var client = NoRedirectClient();

        await client.GetAsync("/" + code);

        var clickCount = await _factory.WithDbContextAsync(async ctx =>
            await ctx.ShortUrls.Where(s => s.ShortCode == UrlShortener.Domain.ShortUrls.ShortCode.Create(code))
                               .Select(s => s.ClickCount)
                               .SingleAsync());
        clickCount.Should().Be(1);
    }

    [Fact]
    public async Task Get_WithValidExistingCode_PersistsClickAuditRow()
    {
        const string code = "Redir03";
        await _factory.SeedShortUrlAsync(code);
        var client = NoRedirectClient();

        await client.GetAsync("/" + code);

        var auditCount = await _factory.WithDbContextAsync(async ctx =>
            await ctx.ClickAudits.CountAsync(a => a.ShortCodeValue == code));
        auditCount.Should().Be(1);
    }

    [Fact(Skip = "Route constraint length(7):regex(^[A-Za-z0-9]+$) prevents the handler from receiving an invalid format; cannot reach the 400 path")]
    public Task Get_WithInvalidCodeFormat_Returns400() => Task.CompletedTask;

    [Fact]
    public async Task Get_WithMissingCode_Returns404()
    {
        var client = NoRedirectClient();

        var response = await client.GetAsync("/Missin0");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("ShortUrl.NotFound");
    }

    [Fact]
    public async Task Get_WithDisabledShortUrl_Returns410()
    {
        const string code = "Redir04";
        await _factory.SeedShortUrlAsync(code, isEnabled: false);
        var client = NoRedirectClient();

        var response = await client.GetAsync("/" + code);

        response.StatusCode.Should().Be(HttpStatusCode.Gone);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("ShortUrl.Disabled");
    }

    [Fact]
    public async Task Get_WithExpiredShortUrl_Returns410()
    {
        const string code = "Redir05";
        var future = DateTime.UtcNow.AddMinutes(5);
        await _factory.SeedShortUrlAsync(code, expiresAt: future);
        _factory.Clock.SetUtcNow(future.AddMinutes(1));
        var client = NoRedirectClient();

        var response = await client.GetAsync("/" + code);

        response.StatusCode.Should().Be(HttpStatusCode.Gone);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("ShortUrl.Expired");

        _factory.Clock.Reset();
    }

    [Fact]
    public async Task Get_WithCodeShorterThan7Chars_Returns404DueToRouteConstraint()
    {
        var client = NoRedirectClient();

        var response = await client.GetAsync("/abc");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_WithCodeLongerThan7Chars_Returns404DueToRouteConstraint()
    {
        var client = NoRedirectClient();

        var response = await client.GetAsync("/abcdefghij");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_WithNonAlphanumericCode_Returns404DueToRouteConstraint()
    {
        var client = NoRedirectClient();

        var response = await client.GetAsync("/abc-123");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private HttpClient NoRedirectClient() =>
        _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
}
