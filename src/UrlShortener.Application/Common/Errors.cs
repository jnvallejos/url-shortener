namespace UrlShortener.Application.Common;

public static class Errors
{
    public static class ShortUrl
    {
        public static readonly Error NotFound =
            new("ShortUrl.NotFound", "Short URL not found");

        public static readonly Error Disabled =
            new("ShortUrl.Disabled", "Short URL is disabled");

        public static readonly Error Expired =
            new("ShortUrl.Expired", "Short URL has expired");

        public static Error CodeAlreadyExists(string code) =>
            new("ShortUrl.CodeAlreadyExists",
                $"Short code '{code}' already exists");

        public static readonly Error CodeGenerationFailed =
            new("ShortUrl.CodeGenerationFailed",
                "Could not generate a unique short code after maximum retries");
    }

    public static class OriginalUrl
    {
        public static Error Invalid(string reason) =>
            new("OriginalUrl.Invalid", reason);
    }

    public static class ShortCode
    {
        public static Error Invalid(string reason) =>
            new("ShortCode.Invalid", reason);
    }

    public static class Validation
    {
        public static Error InvalidExpiration(string reason) =>
            new("Validation.InvalidExpiration", reason);
    }
}
