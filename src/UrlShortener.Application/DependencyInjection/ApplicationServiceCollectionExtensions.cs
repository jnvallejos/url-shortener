using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Application.ShortUrls.Admin.Disable;
using UrlShortener.Application.ShortUrls.Admin.Enable;
using UrlShortener.Application.ShortUrls.Admin.UpdateExpiration;
using UrlShortener.Application.ShortUrls.Create;
using UrlShortener.Application.ShortUrls.GetByCode;
using UrlShortener.Application.ShortUrls.Redirect;

namespace UrlShortener.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateShortUrlUseCase>();
        services.AddScoped<RedirectUseCase>();
        services.AddScoped<GetShortUrlUseCase>();
        services.AddScoped<DisableShortUrlUseCase>();
        services.AddScoped<EnableShortUrlUseCase>();
        services.AddScoped<UpdateExpirationUseCase>();

        return services;
    }
}
