using FluentAssertions;
using UrlShortener.Application.Common;

namespace UrlShortener.Application.Tests.Common;

public class ErrorTests
{
    [Fact]
    public void Equals_TwoErrorsWithSameCodeAndMessage_AreEqual()
    {
        var a = new Error("Code.X", "message");
        var b = new Error("Code.X", "message");

        a.Should().Be(b);
    }

    [Fact]
    public void Equals_TwoErrorsWithDifferentCode_AreNotEqual()
    {
        var a = new Error("Code.X", "message");
        var b = new Error("Code.Y", "message");

        a.Should().NotBe(b);
    }

    [Fact]
    public void Equals_TwoErrorsWithDifferentMessage_AreNotEqual()
    {
        var a = new Error("Code.X", "messageA");
        var b = new Error("Code.X", "messageB");

        a.Should().NotBe(b);
    }

    [Fact]
    public void None_HasEmptyCodeAndEmptyMessage()
    {
        Error.None.Code.Should().BeEmpty();
        Error.None.Message.Should().BeEmpty();
    }
}
