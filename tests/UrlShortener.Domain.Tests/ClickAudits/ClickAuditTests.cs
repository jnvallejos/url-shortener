using FluentAssertions;
using UrlShortener.Domain.ClickAudits;

namespace UrlShortener.Domain.Tests.ClickAudits;

public class ClickAuditTests
{
    [Fact]
    public void Create_WithValidInputs_ReturnsClickAudit()
    {
        var shortUrlId = Guid.NewGuid();
        var clickedAt = DateTime.UtcNow;

        var audit = ClickAudit.Create(shortUrlId, "abc1234", clickedAt, "ua", "1.2.3.4");

        audit.Should().NotBeNull();
        audit.Id.Should().NotBe(Guid.Empty);
        audit.ShortUrlId.Should().Be(shortUrlId);
        audit.ShortCodeValue.Should().Be("abc1234");
        audit.ClickedAt.Should().Be(clickedAt);
        audit.UserAgent.Should().Be("ua");
        audit.IpAddress.Should().Be("1.2.3.4");
    }

    [Fact]
    public void Create_WithNullUserAgent_AllowsCreation()
    {
        var audit = ClickAudit.Create(Guid.NewGuid(), "abc1234", DateTime.UtcNow, null, "1.2.3.4");

        audit.UserAgent.Should().BeNull();
    }

    [Fact]
    public void Create_WithNullIpAddress_AllowsCreation()
    {
        var audit = ClickAudit.Create(Guid.NewGuid(), "abc1234", DateTime.UtcNow, "ua", null);

        audit.IpAddress.Should().BeNull();
    }

    [Fact]
    public void Create_WithUserAgentLongerThan512_TruncatesTo512()
    {
        var longUa = new string('x', 1000);

        var audit = ClickAudit.Create(Guid.NewGuid(), "abc1234", DateTime.UtcNow, longUa, null);

        audit.UserAgent!.Length.Should().Be(512);
    }

    [Fact]
    public void Create_WithIpAddressLongerThan45_TruncatesTo45()
    {
        var longIp = new string('y', 100);

        var audit = ClickAudit.Create(Guid.NewGuid(), "abc1234", DateTime.UtcNow, null, longIp);

        audit.IpAddress!.Length.Should().Be(45);
    }

    [Fact]
    public void Create_WithIpv6Address_PreservesFullAddress()
    {
        // 39-char IPv6, well under the 45-char cap.
        const string ipv6 = "2001:0db8:85a3:0000:0000:8a2e:0370:7334";

        var audit = ClickAudit.Create(Guid.NewGuid(), "abc1234", DateTime.UtcNow, null, ipv6);

        audit.IpAddress.Should().Be(ipv6);
    }

    [Fact]
    public void Equals_TwoClickAuditsWithSameValues_AreEqual()
    {
        var audit = ClickAudit.Create(Guid.NewGuid(), "abc1234", DateTime.UtcNow, "ua", "1.2.3.4");
        var clone = audit with { };

        audit.Equals(clone).Should().BeTrue();
        (audit == clone).Should().BeTrue();
    }

    [Fact]
    public void Equals_TwoClickAuditsWithDifferentIds_AreNotEqual()
    {
        var shortUrlId = Guid.NewGuid();
        var clickedAt = DateTime.UtcNow;

        var a = ClickAudit.Create(shortUrlId, "abc1234", clickedAt, "ua", "1.2.3.4");
        var b = ClickAudit.Create(shortUrlId, "abc1234", clickedAt, "ua", "1.2.3.4");

        a.Equals(b).Should().BeFalse();
        (a != b).Should().BeTrue();
    }
}
