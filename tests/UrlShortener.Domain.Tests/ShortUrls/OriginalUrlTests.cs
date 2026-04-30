using FluentAssertions;
using UrlShortener.Domain.Exceptions;
using UrlShortener.Domain.ShortUrls;

namespace UrlShortener.Domain.Tests.ShortUrls;

public class OriginalUrlTests
{
    [Fact]
    public void Create_WithValidHttpsUrl_ReturnsOriginalUrl()
    {
        var url = OriginalUrl.Create("https://example.com/path");

        url.Should().NotBeNull();
        url.ToString().Should().Be("https://example.com/path");
    }

    [Fact]
    public void Create_WithValidHttpUrl_ReturnsOriginalUrl()
    {
        var url = OriginalUrl.Create("http://example.com");

        url.ToString().Should().StartWith("http://example.com");
    }

    [Fact]
    public void Create_WithUppercaseScheme_NormalizesToLowercase()
    {
        var url = OriginalUrl.Create("HTTP://Example.COM/Path");

        url.ToString().Should().StartWith("http://");
    }

    [Fact]
    public void Create_WithLeadingAndTrailingWhitespace_TrimsBeforeValidation()
    {
        var url = OriginalUrl.Create("  https://example.com  ");

        url.ToString().Should().StartWith("https://example.com");
    }
}
