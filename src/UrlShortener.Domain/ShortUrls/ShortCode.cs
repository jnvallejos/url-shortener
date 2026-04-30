using UrlShortener.Domain.Common;
using UrlShortener.Domain.Exceptions;

namespace UrlShortener.Domain.ShortUrls;

public sealed class ShortCode : ValueObject
{
    public const int RequiredLength = 7;

    private readonly string _value;

    private ShortCode(string value)
    {
        _value = value;
    }

    public static ShortCode Create(string value)
    {
        if (value is null || value.Length != RequiredLength)
        {
            var actualLength = value?.Length ?? 0;
            throw new InvalidShortCodeException(
                $"ShortCode must be exactly {RequiredLength} characters; received '{value ?? "<null>"}' with length {actualLength}");
        }

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (!IsBase62(c))
            {
                throw new InvalidShortCodeException(
                    $"ShortCode contains invalid character at position {i}: '{c}'. Allowed: [A-Za-z0-9]");
            }
        }

        return new ShortCode(value);
    }

    private static bool IsBase62(char c) =>
        (c >= '0' && c <= '9') ||
        (c >= 'A' && c <= 'Z') ||
        (c >= 'a' && c <= 'z');

    public override string ToString() => _value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return _value;
    }
}
