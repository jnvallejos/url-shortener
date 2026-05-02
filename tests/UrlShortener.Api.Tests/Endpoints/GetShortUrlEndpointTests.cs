using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using UrlShortener.Api.Contracts;
using UrlShortener.Api.Tests.TestSupport;

namespace UrlShortener.Api.Tests.Endpoints;

public class GetShortUrlEndpointTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public GetShortUrlEndpointTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Clock.Reset();
    }

    [Fact]
    public async Task Get_WithExistingCode_Returns200WithFullDetails()
    {
        const string code = "GetSh01";
        await _factory.SeedShortUrlAsync(code, originalUrl: "https://example.com/get1");
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/shorturls/{code}");
        var body = await response.Content.ReadFromJsonAsync<ShortUrlContract>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.ShortCode.Should().Be(code);
        body.OriginalUrl.Should().Be("https://example.com/get1");
        body.ShortUrl.Should().EndWith("/" + code);
        body.IsEnabled.Should().BeTrue();
        body.ClickCount.Should().Be(0);
    }

    [Fact]
    public async Task Get_WithDisabledShortUrl_Returns200WithIsEnabledFalse()
    {
        const string code = "GetSh02";
        await _factory.SeedShortUrlAsync(code, isEnabled: false);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/shorturls/{code}");
        var body = await response.Content.ReadFromJsonAsync<ShortUrlContract>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Get_WithExpiredShortUrl_Returns200WithExpiresAtInPast()
    {
        const string code = "GetSh03";
        await _factory.SeedShortUrlAsync(code, expiresAt: DateTime.UtcNow.AddDays(1));
        var pastDate = DateTime.UtcNow.AddHours(-1);
        await _factory.SetExpirationRawAsync(code, pastDate);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/shorturls/{code}");
        var body = await response.Content.ReadFromJsonAsync<ShortUrlContract>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.ExpiresAt.Should().NotBeNull();
        body.ExpiresAt!.Value.Should().BeBefore(DateTime.UtcNow);
    }

    [Fact]
    public async Task Get_WithMissingCode_Returns404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/shorturls/Missin1");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("ShortUrl.NotFound");
    }

    [Fact]
    public async Task Get_WithInvalidCodeFormat_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/shorturls/bad-code-format");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("ShortCode.Invalid");
    }
}
