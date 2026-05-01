using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Application.Abstractions;
using UrlShortener.Domain.ShortUrls.Events;
using UrlShortener.Infrastructure.DependencyInjection;
using UrlShortener.Infrastructure.Events;
using UrlShortener.Infrastructure.Persistence;

namespace UrlShortener.Infrastructure.Tests.DependencyInjection;

public class InfrastructureServiceCollectionExtensionsTests
{
    private const string DummyConnectionString =
        "Host=localhost;Database=urlshortener_test;Username=postgres;Password=postgres";

    [Fact]
    public void AddInfrastructure_RegistersApplicationDbContext()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure(DummyConnectionString);

        services.Should().Contain(d => d.ServiceType == typeof(ApplicationDbContext));
    }

    [Fact]
    public void AddInfrastructure_RegistersShortUrlRepository()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure(DummyConnectionString);

        services.Should().Contain(d => d.ServiceType == typeof(IShortUrlRepository));
    }

    [Fact]
    public void AddInfrastructure_RegistersShortCodeGenerator()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure(DummyConnectionString);

        services.Should().Contain(d => d.ServiceType == typeof(IShortCodeGenerator));
    }

    [Fact]
    public void AddInfrastructure_RegistersDateTimeProvider()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure(DummyConnectionString);

        services.Should().Contain(d => d.ServiceType == typeof(IDateTimeProvider));
    }

    [Fact]
    public void AddInfrastructure_RegistersDomainEventDispatcher()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure(DummyConnectionString);

        services.Should().Contain(d => d.ServiceType == typeof(IDomainEventDispatcher));
    }

    [Fact]
    public void AddInfrastructure_RegistersShortUrlClickedEventHandler()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure(DummyConnectionString);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IDomainEventHandler<ShortUrlClickedEvent>));
    }

    [Fact]
    public void AddInfrastructure_AllAbstractions_ResolveFromProvider()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(DummyConnectionString);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IShortUrlRepository>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IShortCodeGenerator>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>().Should().NotBeNull();
        scope.ServiceProvider
            .GetRequiredService<IDomainEventHandler<ShortUrlClickedEvent>>()
            .Should().NotBeNull();
    }

    [Fact]
    public void AddInfrastructure_DbContextUsesNpgsql()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(DummyConnectionString);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ctx.Database.ProviderName.Should().Be("Npgsql.EntityFrameworkCore.PostgreSQL");
    }
}
