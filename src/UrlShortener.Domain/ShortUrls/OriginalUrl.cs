using UrlShortener.Domain.Common;
using UrlShortener.Domain.Exceptions;

namespace UrlShortener.Domain.ShortUrls;

public sealed class OriginalUrl : ValueObject
{
    public const int MaxLength = 2048;

    private readonly string _value;

    private OriginalUrl(string value)
    {
        _value = value;
    }

    public static OriginalUrl Create(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            throw new InvalidOriginalUrlException(
                "OriginalUrl must not be null, empty, or whitespace");
        }

        if (trimmed.Length > MaxLength)
        {
            throw new InvalidOriginalUrlException(
                $"OriginalUrl exceeds maximum length of {MaxLength} characters; received length: {trimmed.Length}");
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            throw new InvalidOriginalUrlException(
                $"OriginalUrl could not be parsed as an absolute URI; received: '{trimmed}'");
        }

        var scheme = uri.Scheme;
        if (!string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOriginalUrlException(
                $"OriginalUrl scheme must be 'http' or 'https'; received: '{scheme}'");
        }

        return new OriginalUrl(uri.AbsoluteUri);
    }

    public override string ToString() => _value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return _value;
    }
}
