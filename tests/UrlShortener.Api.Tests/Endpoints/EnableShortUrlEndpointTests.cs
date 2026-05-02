using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using UrlShortener.Api.Contracts;
using UrlShortener.Api.Tests.TestSupport;

namespace UrlShortener.Api.Tests.Endpoints;

public class EnableShortUrlEndpointTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public EnableShortUrlEndpointTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Clock.Reset();
    }

    [Fact]
    public async Task Post_WithExistingCode_Returns200WithUpdatedState()
    {
        const string code = "Enabl01";
        await _factory.SeedShortUrlAsync(code, isEnabled: false);
        var client = _factory.CreateClient();

        var response = await client.PostAsync($"/api/shorturls/{code}/enable", content: null);
        var body = await response.Content.ReadFromJsonAsync<ShortUrlStateContract>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Code.Should().Be(code);
        body.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Post_OnAlreadyTargetState_StillReturns200()
    {
        const string code = "Enabl02";
        await _factory.SeedShortUrlAsync(code, isEnabled: true);
        var client = _factory.CreateClient();

        var response = await client.PostAsync($"/api/shorturls/{code}/enable", content: null);
        var body = await response.Content.ReadFromJsonAsync<ShortUrlStateContract>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Post_WithMissingCode_Returns404()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/shorturls/Misnf02/enable", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("ShortUrl.NotFound");
    }

    [Fact]
    public async Task Post_WithInvalidCodeFormat_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/shorturls/bad-code/enable", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("ShortCode.Invalid");
    }
}
