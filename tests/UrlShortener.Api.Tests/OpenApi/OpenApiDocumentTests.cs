using System.Net;
using System.Text.Json;
using FluentAssertions;
using UrlShortener.Api.Tests.TestSupport;

namespace UrlShortener.Api.Tests.OpenApi;

public class OpenApiDocumentTests : IClassFixture<DevelopmentApiFactory>
{
    private readonly DevelopmentApiFactory _factory;

    public OpenApiDocumentTests(DevelopmentApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_OpenApiJson_Returns200WithValidJson()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Action parse = () => JsonDocument.Parse(content);
        parse.Should().NotThrow();
    }

    [Fact]
    public async Task OpenApiDocument_ContainsAllSixEndpoints()
    {
        var doc = await LoadDocumentAsync();

        var pathNames = doc.RootElement.GetProperty("paths").EnumerateObject()
            .Select(p => p.Name).ToList();

        pathNames.Should().Contain(p => p.StartsWith("/api/shorturls") && !p.Contains("{code}"), "CreateShortUrl path is registered");
        pathNames.Should().Contain(p => p.StartsWith("/api/shorturls/{code}") && !p.Contains("disable") && !p.Contains("enable") && !p.Contains("expiration"), "GetShortUrl path is registered");
        pathNames.Should().Contain(p => p.Contains("/api/shorturls/{code}/disable"), "DisableShortUrl path is registered");
        pathNames.Should().Contain(p => p.Contains("/api/shorturls/{code}/enable"), "EnableShortUrl path is registered");
        pathNames.Should().Contain(p => p.Contains("/api/shorturls/{code}/expiration"), "UpdateExpiration path is registered");
        pathNames.Should().Contain(p => p.Contains("{code}") && !p.StartsWith("/api/"), "Redirect path is registered");
    }

    [Fact]
    public async Task OpenApiDocument_DescribesShortUrlContractSchema()
    {
        var doc = await LoadDocumentAsync();

        var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");
        schemas.TryGetProperty("ShortUrlContract", out _).Should().BeTrue();
    }

    [Fact]
    public async Task OpenApiDocument_DescribesErrorResponseSchema()
    {
        var doc = await LoadDocumentAsync();

        var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");
        schemas.TryGetProperty("ErrorResponse", out _).Should().BeTrue();
    }

    [Fact]
    public async Task OpenApiDocument_TitleIsUrlShortenerApi()
    {
        var doc = await LoadDocumentAsync();

        var info = doc.RootElement.GetProperty("info");
        info.GetProperty("title").GetString().Should().Be("URL Shortener API");
    }

    private async Task<JsonDocument> LoadDocumentAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }
}
