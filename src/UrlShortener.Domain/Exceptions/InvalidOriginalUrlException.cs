namespace UrlShortener.Domain.Exceptions;

public sealed class InvalidOriginalUrlException : DomainException
{
    public InvalidOriginalUrlException(string message) : base(message) { }
}
