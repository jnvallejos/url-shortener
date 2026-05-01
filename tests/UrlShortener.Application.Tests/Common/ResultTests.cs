using FluentAssertions;
using UrlShortener.Application.Common;

namespace UrlShortener.Application.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_NonGeneric_ReturnsResultWithIsSuccessTrueAndErrorNone()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Success_Generic_ReturnsResultWithValueAndIsSuccessTrue()
    {
        var result = Result.Success("payload");

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
        result.Value.Should().Be("payload");
    }

    [Fact]
    public void Failure_NonGeneric_ReturnsResultWithIsFailureTrueAndError()
    {
        var error = new Error("Code.X", "message");

        var result = Result.Failure(error);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Failure_Generic_ReturnsResultWithIsFailureTrueAndError()
    {
        var error = new Error("Code.X", "message");

        var result = Result.Failure<string>(error);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Constructor_SuccessWithNonNoneError_ThrowsInvalidOperationException()
    {
        var error = new Error("Code.X", "message");

        Action act = () => _ = new TestableResult(isSuccess: true, error: error);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_FailureWithNoneError_ThrowsInvalidOperationException()
    {
        Action act = () => _ = new TestableResult(isSuccess: false, error: Error.None);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Value_OnFailedResult_ThrowsInvalidOperationException()
    {
        var failed = Result.Failure<string>(new Error("Code.X", "message"));

        Action act = () => _ = failed.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ImplicitConversion_FromValue_CreatesSuccessResult()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void ImplicitConversion_FromError_CreatesFailureResult()
    {
        var error = new Error("Code.X", "message");

        Result<string> result = error;

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    private sealed class TestableResult : Result
    {
        public TestableResult(bool isSuccess, Error error) : base(isSuccess, error)
        {
        }
    }
}
