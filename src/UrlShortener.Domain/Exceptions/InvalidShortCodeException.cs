namespace UrlShortener.Domain.Exceptions;

public sealed class InvalidShortCodeException : DomainException
{
    public InvalidShortCodeException(string message) : base(message) { }
}
