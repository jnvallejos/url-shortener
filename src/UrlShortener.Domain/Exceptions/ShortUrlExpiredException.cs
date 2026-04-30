namespace UrlShortener.Domain.Exceptions;

public sealed class ShortUrlExpiredException : DomainException
{
    public ShortUrlExpiredException(string message) : base(message) { }
}
