using FluentAssertions;
using UrlShortener.Domain.ShortUrls;

namespace UrlShortener.Domain.Tests.ShortUrls;

public class ShortCodeTests
{
    [Fact]
    public void Create_WithValid7CharBase62String_ReturnsShortCode()
    {
        var shortCode = ShortCode.Create("abc1234");

        shortCode.Should().NotBeNull();
        shortCode.ToString().Should().Be("abc1234");
    }

    [Fact]
    public void Create_WithExactly7AlphanumericChars_ReturnsShortCode()
    {
        var shortCode = ShortCode.Create("AaZz019");

        shortCode.ToString().Should().Be("AaZz019");
    }

    [Fact]
    public void ToString_ReturnsUnderlyingValue()
    {
        var shortCode = ShortCode.Create("xYz0123");

        shortCode.ToString().Should().Be("xYz0123");
    }
}
