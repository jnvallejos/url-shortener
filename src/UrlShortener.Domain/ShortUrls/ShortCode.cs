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

        return new ShortCode(value);
    }

    public override string ToString() => _value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return _value;
    }
}
