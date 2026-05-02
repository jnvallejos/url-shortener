using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using UrlShortener.Api.Contracts;
using UrlShortener.Api.Tests.TestSupport;

namespace UrlShortener.Api.Tests.Endpoints;

public class CreateShortUrlEndpointTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public CreateShortUrlEndpointTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_WithValidRequest_Returns201CreatedWithLocationHeader()
    {
        var client = _factory.CreateClient();
        var request = new CreateShortUrlContract(
            OriginalUrl: "https://example.com/post-201-test",
            CustomCode: null,
            ExpiresAt: null);

        var response = await client.PostAsJsonAsync("/api/shorturls", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().StartWith("/api/shorturls/");
    }

    [Fact]
    public async Task Post_WithValidRequest_ResponseBodyContainsExpectedFields()
    {
        var client = _factory.CreateClient();
        var request = new CreateShortUrlContract(
            OriginalUrl: "https://example.com/post-body-test",
            CustomCode: null,
            ExpiresAt: null);

        var response = await client.PostAsJsonAsync("/api/shorturls", request);
        var body = await response.Content.ReadFromJsonAsync<ShortUrlContract>();

        body.Should().NotBeNull();
        body!.Id.Should().NotBeEmpty();
        body.ShortCode.Should().HaveLength(7);
        body.OriginalUrl.Should().Be("https://example.com/post-body-test");
        body.ShortUrl.Should().EndWith("/" + body.ShortCode);
        body.ExpiresAt.Should().BeNull();
        body.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        body.IsEnabled.Should().BeTrue();
        body.ClickCount.Should().Be(0);
    }

    [Fact]
    public async Task Post_WithCustomCode_UsesProvidedCode()
    {
        var client = _factory.CreateClient();
        var request = new CreateShortUrlContract(
            OriginalUrl: "https://example.com/custom-code",
            CustomCode: "Custom1",
            ExpiresAt: null);

        var response = await client.PostAsJsonAsync("/api/shorturls", request);
        var body = await response.Content.ReadFromJsonAsync<ShortUrlContract>();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        body.Should().NotBeNull();
        body!.ShortCode.Should().Be("Custom1");
    }

    [Fact]
    public async Task Post_WithDuplicateCustomCode_Returns409Conflict()
    {
        var client = _factory.CreateClient();
        var first = new CreateShortUrlContract(
            OriginalUrl: "https://example.com/dup-1",
            CustomCode: "Dupcode",
            ExpiresAt: null);
        var second = first with { OriginalUrl = "https://example.com/dup-2" };

        var firstResponse = await client.PostAsJsonAsync("/api/shorturls", first);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var conflictResponse = await client.PostAsJsonAsync("/api/shorturls", second);

        conflictResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await conflictResponse.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("ShortUrl.CodeAlreadyExists");
    }

    [Fact]
    public async Task Post_WithInvalidOriginalUrl_Returns400WithErrorCode()
    {
        var client = _factory.CreateClient();
        var request = new CreateShortUrlContract(
            OriginalUrl: "not-a-real-url",
            CustomCode: null,
            ExpiresAt: null);

        var response = await client.PostAsJsonAsync("/api/shorturls", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("OriginalUrl.Invalid");
    }

    [Fact]
    public async Task Post_WithExpiresAtInPast_Returns400WithInvalidExpirationCode()
    {
        var client = _factory.CreateClient();
        var request = new CreateShortUrlContract(
            OriginalUrl: "https://example.com/past-exp",
            CustomCode: null,
            ExpiresAt: DateTime.UtcNow.AddDays(-1));

        var response = await client.PostAsJsonAsync("/api/shorturls", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("Validation.InvalidExpiration");
    }

    [Fact]
    public async Task Post_WithoutBody_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/shorturls", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
