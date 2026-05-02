using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using UrlShortener.Api.Contracts;
using UrlShortener.Api.Tests.TestSupport;

namespace UrlShortener.Api.Tests.Endpoints;

public class UpdateExpirationEndpointTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public UpdateExpirationEndpointTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Clock.Reset();
    }

    [Fact]
    public async Task Patch_WithValidFutureDate_Returns200WithUpdatedExpiresAt()
    {
        const string code = "Updxp01";
        await _factory.SeedShortUrlAsync(code);
        var client = _factory.CreateClient();
        var future = DateTime.UtcNow.AddDays(7);

        var response = await client.PatchAsJsonAsync(
            $"/api/shorturls/{code}/expiration",
            new UpdateExpirationContract(future));
        var body = await response.Content.ReadFromJsonAsync<ShortUrlExpirationContract>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Code.Should().Be(code);
        body.ExpiresAt.Should().NotBeNull();
        body.ExpiresAt!.Value.Should().BeCloseTo(future, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Patch_WithNullExpiresAt_Returns200AndClearsExpiration()
    {
        const string code = "Updxp02";
        await _factory.SeedShortUrlAsync(code, expiresAt: DateTime.UtcNow.AddDays(1));
        var client = _factory.CreateClient();

        var response = await client.PatchAsJsonAsync(
            $"/api/shorturls/{code}/expiration",
            new UpdateExpirationContract(null));
        var body = await response.Content.ReadFromJsonAsync<ShortUrlExpirationContract>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task Patch_WithPastDate_Returns400WithInvalidExpirationCode()
    {
        const string code = "Updxp03";
        await _factory.SeedShortUrlAsync(code);
        var client = _factory.CreateClient();

        var response = await client.PatchAsJsonAsync(
            $"/api/shorturls/{code}/expiration",
            new UpdateExpirationContract(DateTime.UtcNow.AddDays(-1)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("Validation.InvalidExpiration");
    }

    [Fact]
    public async Task Patch_OnDisabledShortUrl_AllowsUpdate()
    {
        const string code = "Updxp04";
        await _factory.SeedShortUrlAsync(code, isEnabled: false);
        var client = _factory.CreateClient();
        var future = DateTime.UtcNow.AddDays(3);

        var response = await client.PatchAsJsonAsync(
            $"/api/shorturls/{code}/expiration",
            new UpdateExpirationContract(future));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patch_OnExpiredShortUrl_AllowsUpdate()
    {
        const string code = "Updxp05";
        await _factory.SeedShortUrlAsync(code, expiresAt: DateTime.UtcNow.AddDays(1));
        await _factory.SetExpirationRawAsync(code, DateTime.UtcNow.AddHours(-1));
        var client = _factory.CreateClient();
        var future = DateTime.UtcNow.AddDays(7);

        var response = await client.PatchAsJsonAsync(
            $"/api/shorturls/{code}/expiration",
            new UpdateExpirationContract(future));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patch_WithMissingCode_Returns404()
    {
        var client = _factory.CreateClient();

        var response = await client.PatchAsJsonAsync(
            "/api/shorturls/Misnf03/expiration",
            new UpdateExpirationContract(DateTime.UtcNow.AddDays(1)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("ShortUrl.NotFound");
    }

    [Fact]
    public async Task Patch_WithInvalidCodeFormat_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PatchAsJsonAsync(
            "/api/shorturls/bad-code/expiration",
            new UpdateExpirationContract(DateTime.UtcNow.AddDays(1)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("ShortCode.Invalid");
    }
}
