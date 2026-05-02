namespace UrlShortener.Api.Contracts;

public sealed record ShortUrlStateContract(string Code, bool IsEnabled);
