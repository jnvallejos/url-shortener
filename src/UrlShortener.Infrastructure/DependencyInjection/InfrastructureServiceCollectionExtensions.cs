using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Application.Abstractions;
using UrlShortener.Domain.ShortUrls.Events;
using UrlShortener.Infrastructure.Codes;
using UrlShortener.Infrastructure.Events;
using UrlShortener.Infrastructure.Events.Handlers;
using UrlShortener.Infrastructure.Persistence;
using UrlShortener.Infrastructure.Persistence.Repositories;
using UrlShortener.Infrastructure.Time;

namespace UrlShortener.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IShortUrlRepository, EfShortUrlRepository>();
        services.AddSingleton<IShortCodeGenerator, Base62ShortCodeGenerator>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        services.AddScoped<
            IDomainEventHandler<ShortUrlClickedEvent>,
            ShortUrlClickedEventHandler>();

        return services;
    }
}
