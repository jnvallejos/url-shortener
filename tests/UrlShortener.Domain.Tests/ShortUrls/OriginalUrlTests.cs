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

    [Fact]
    public void Create_WithJavascriptScheme_ThrowsInvalidOriginalUrlException()
    {
        Action act = () => OriginalUrl.Create("javascript:alert(1)");

        act.Should().Throw<InvalidOriginalUrlException>();
    }

    [Fact]
    public void Create_WithDataScheme_ThrowsInvalidOriginalUrlException()
    {
        Action act = () => OriginalUrl.Create("data:text/html,<script>");

        act.Should().Throw<InvalidOriginalUrlException>();
    }

    [Fact]
    public void Create_WithFileScheme_ThrowsInvalidOriginalUrlException()
    {
        Action act = () => OriginalUrl.Create("file:///c:/secret.txt");

        act.Should().Throw<InvalidOriginalUrlException>();
    }

    [Fact]
    public void Create_WithFtpScheme_ThrowsInvalidOriginalUrlException()
    {
        Action act = () => OriginalUrl.Create("ftp://example.com");

        act.Should().Throw<InvalidOriginalUrlException>();
    }

    [Fact]
    public void Create_WithRelativeUrl_ThrowsInvalidOriginalUrlException()
    {
        Action act = () => OriginalUrl.Create("/just/a/path");

        act.Should().Throw<InvalidOriginalUrlException>();
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("http://")]
    [InlineData("://example.com")]
    public void Create_WithMalformedUrl_ThrowsInvalidOriginalUrlException(string input)
    {
        Action act = () => OriginalUrl.Create(input);

        act.Should().Throw<InvalidOriginalUrlException>();
    }

    [Fact]
    public void Create_WithIdnDomain_NormalizesToPunycode()
    {
        var url = OriginalUrl.Create("https://例え.jp");

        url.ToString().Should().Contain("xn--");
        url.ToString().Should().StartWith("https://");
    }

    [Fact]
    public void Create_WithUnicodePath_PreservesPath()
    {
        var url = OriginalUrl.Create("https://example.com/café");

        url.ToString().Should().StartWith("https://example.com/");
        // Unicode path is percent-encoded into the canonical AbsoluteUri form.
        url.ToString().Should().Contain("caf");
    }

    [Fact]
    public void Create_WithQueryStringAndFragment_PreservesBoth()
    {
        var url = OriginalUrl.Create("https://example.com/path?q=1&n=2#section");

        var s = url.ToString();
        s.Should().Contain("?q=1&n=2");
        s.Should().Contain("#section");
    }

    [Fact]
    public void ToString_ReturnsNormalizedAbsoluteUri()
    {
        var url = OriginalUrl.Create("HTTPS://EXAMPLE.com/Path");

        // Scheme and host normalize to lowercase; path case is preserved.
        url.ToString().Should().StartWith("https://example.com/");
    }

    [Fact]
    public void Equals_SameUrlDifferentCasing_AreEqual()
    {
        var a = OriginalUrl.Create("HTTPS://Example.COM/Path");
        var b = OriginalUrl.Create("https://example.com/Path");

        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithNull_ReturnsFalse()
    {
        var a = OriginalUrl.Create("https://example.com");
        OriginalUrl? nullUrl = null;
        object? nullObj = null;

        a.Equals(nullUrl).Should().BeFalse();
        a.Equals(nullObj).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentType_ReturnsFalse()
    {
        var a = OriginalUrl.Create("https://example.com");
        object other = "https://example.com/";

        a.Equals(other).Should().BeFalse();
    }

    [Fact]
    public void ExceptionMessage_OnInvalidScheme_ContainsReceivedScheme()
    {
        Action act = () => OriginalUrl.Create("ftp://example.com");

        act.Should().Throw<InvalidOriginalUrlException>()
            .WithMessage("*ftp*");
    }

    [Fact]
    public void ExceptionMessage_OnLengthViolation_ContainsTrimmedLength()
    {
        var prefix = "https://example.com/";
        var input = prefix + new string('a', 2049 - prefix.Length);

        Action act = () => OriginalUrl.Create(input);

        act.Should().Throw<InvalidOriginalUrlException>()
            .WithMessage("*2049*");
    }
}
