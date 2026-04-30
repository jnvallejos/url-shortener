using FluentAssertions;
using UrlShortener.Domain.Exceptions;
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

    [Fact]
    public void Create_WithNullValue_ThrowsInvalidShortCodeException()
    {
        Action act = () => ShortCode.Create(null!);

        act.Should().Throw<InvalidShortCodeException>();
    }

    [Fact]
    public void Create_WithEmptyValue_ThrowsInvalidShortCodeException()
    {
        Action act = () => ShortCode.Create(string.Empty);

        act.Should().Throw<InvalidShortCodeException>();
    }

    [Fact]
    public void Create_With6Chars_ThrowsInvalidShortCodeException()
    {
        Action act = () => ShortCode.Create("abc123");

        act.Should().Throw<InvalidShortCodeException>();
    }

    [Fact]
    public void Create_With8Chars_ThrowsInvalidShortCodeException()
    {
        Action act = () => ShortCode.Create("abc12345");

        act.Should().Throw<InvalidShortCodeException>();
    }

    [Fact]
    public void ExceptionMessage_OnInvalidLength_ContainsActualLength()
    {
        Action act = () => ShortCode.Create("abc12");

        act.Should().Throw<InvalidShortCodeException>()
            .WithMessage("*length 5*");
    }

    [Fact]
    public void Create_WithWhitespaceValue_ThrowsInvalidShortCodeException()
    {
        Action act = () => ShortCode.Create("       ");

        act.Should().Throw<InvalidShortCodeException>();
    }

    [Theory]
    [InlineData("abc-123")]
    [InlineData("abc 123")]
    [InlineData("abc/123")]
    [InlineData("abc_123")]
    public void Create_WithSpecialCharacter_ThrowsInvalidShortCodeException(string value)
    {
        Action act = () => ShortCode.Create(value);

        act.Should().Throw<InvalidShortCodeException>();
    }

    [Fact]
    public void Create_WithUnicodeCharacter_ThrowsInvalidShortCodeException()
    {
        Action act = () => ShortCode.Create("abc12é4");

        act.Should().Throw<InvalidShortCodeException>();
    }

    [Fact]
    public void ExceptionMessage_OnInvalidCharacter_ContainsCharacterAndPosition()
    {
        Action act = () => ShortCode.Create("abc-123");

        act.Should().Throw<InvalidShortCodeException>()
            .WithMessage("*position 3*'-'*");
    }
}
