using FluentAssertions;
using UrlShortener.Infrastructure.Time;

namespace UrlShortener.Infrastructure.Tests.Time;

public class SystemDateTimeProviderTests
{
    private readonly SystemDateTimeProvider _sut = new();

    [Fact]
    public void UtcNow_ReturnsValueCloseToDateTimeUtcNow()
    {
        var before = DateTime.UtcNow;
        var actual = _sut.UtcNow;
        var after = DateTime.UtcNow;

        actual.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void UtcNow_ReturnedKindIsUtc()
    {
        _sut.UtcNow.Kind.Should().Be(DateTimeKind.Utc);
    }
}
