namespace UrlShortener.Application.Abstractions;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
