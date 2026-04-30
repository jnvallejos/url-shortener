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

    [Fact]
    public void Create_WithNullValue_ThrowsInvalidOriginalUrlException()
    {
        Action act = () => OriginalUrl.Create(null!);

        act.Should().Throw<InvalidOriginalUrlException>();
    }

    [Fact]
    public void Create_WithEmptyValue_ThrowsInvalidOriginalUrlException()
    {
        Action act = () => OriginalUrl.Create(string.Empty);

        act.Should().Throw<InvalidOriginalUrlException>();
    }

    [Fact]
    public void Create_WithWhitespaceOnly_ThrowsInvalidOriginalUrlException()
    {
        Action act = () => OriginalUrl.Create("   \t  ");

        act.Should().Throw<InvalidOriginalUrlException>();
    }

    [Fact]
    public void Create_AtExactly2048CharsTrimmed_ReturnsOriginalUrl()
    {
        // "https://example.com/" is 20 chars, so pad path to fill exactly 2048.
        var prefix = "https://example.com/";
        var padding = new string('a', 2048 - prefix.Length);
        var input = prefix + padding;

        var url = OriginalUrl.Create(input);

        url.ToString().Length.Should().Be(2048);
    }

    [Fact]
    public void Create_At2049CharsTrimmed_ThrowsInvalidOriginalUrlException()
    {
        var prefix = "https://example.com/";
        var padding = new string('a', 2049 - prefix.Length);
        var input = prefix + padding;

        Action act = () => OriginalUrl.Create(input);

        act.Should().Throw<InvalidOriginalUrlException>();
    }

    [Fact]
    public void Create_WithRawLength2050ButTrimmedLength2046_ReturnsOriginalUrl()
    {
        var prefix = "https://example.com/";
        var padding = new string('a', 2046 - prefix.Length);
        var trimmed = prefix + padding;
        var input = "  " + trimmed + "  "; // raw 2050, trimmed 2046

        input.Length.Should().Be(2050);
        trimmed.Length.Should().Be(2046);

        var url = OriginalUrl.Create(input);

        url.ToString().Length.Should().Be(2046);
    }
}
