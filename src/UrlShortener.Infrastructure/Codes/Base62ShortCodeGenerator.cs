using System.Security.Cryptography;
using UrlShortener.Application.Abstractions;
using UrlShortener.Domain.ShortUrls;

namespace UrlShortener.Infrastructure.Codes;

public sealed class Base62ShortCodeGenerator : IShortCodeGenerator
{
    private const string Alphabet =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int CodeLength = 7;

    public Task<ShortCode> GenerateAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        Span<byte> bytes = stackalloc byte[CodeLength];
        RandomNumberGenerator.Fill(bytes);

        Span<char> chars = stackalloc char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        return Task.FromResult(ShortCode.Create(new string(chars)));
    }
}
