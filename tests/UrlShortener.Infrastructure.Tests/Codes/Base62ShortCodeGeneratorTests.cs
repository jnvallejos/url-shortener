using FluentAssertions;
using UrlShortener.Domain.ShortUrls;
using UrlShortener.Infrastructure.Codes;

namespace UrlShortener.Infrastructure.Tests.Codes;

public class Base62ShortCodeGeneratorTests
{
    private const string Alphabet =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    private readonly Base62ShortCodeGenerator _sut = new();

    [Fact]
    public async Task GenerateAsync_ReturnsShortCodeWithLengthSeven()
    {
        var code = await _sut.GenerateAsync(CancellationToken.None);

        code.ToString().Length.Should().Be(ShortCode.RequiredLength);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsBase62OnlyCharacters()
    {
        var code = await _sut.GenerateAsync(CancellationToken.None);

        code.ToString().ToCharArray().Should().OnlyContain(c => Alphabet.Contains(c));
    }

    [Fact]
    public async Task GenerateAsync_TwoSequentialCalls_ReturnDifferentCodes()
    {
        var first = await _sut.GenerateAsync(CancellationToken.None);
        var second = await _sut.GenerateAsync(CancellationToken.None);

        first.Should().NotBe(second);
    }

    [Fact]
    public async Task GenerateAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => _sut.GenerateAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GenerateAsync_HighVolumeRun_ProducesReasonableCharacterDistribution()
    {
        const int runs = 10_000;
        const int codeLength = 7;
        const int totalCharacters = runs * codeLength;
        var counts = new int[Alphabet.Length];

        for (var i = 0; i < runs; i++)
        {
            var code = (await _sut.GenerateAsync(CancellationToken.None)).ToString();
            for (var j = 0; j < codeLength; j++)
            {
                var index = Alphabet.IndexOf(code[j]);
                counts[index]++;
            }
        }

        var expectedPerCharacter = totalCharacters / (double)Alphabet.Length;
        var minimumAcceptable = expectedPerCharacter * 0.5;

        counts.Should().OnlyContain(c => c >= minimumAcceptable);
    }
}
