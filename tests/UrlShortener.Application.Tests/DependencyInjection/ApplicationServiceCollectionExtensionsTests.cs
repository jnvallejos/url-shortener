using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UrlShortener.Application.Abstractions;
using UrlShortener.Application.DependencyInjection;
using UrlShortener.Application.ShortUrls.Admin.Disable;
using UrlShortener.Application.ShortUrls.Admin.Enable;
using UrlShortener.Application.ShortUrls.Admin.UpdateExpiration;
using UrlShortener.Application.ShortUrls.Create;
using UrlShortener.Application.ShortUrls.GetByCode;
using UrlShortener.Application.ShortUrls.Redirect;

namespace UrlShortener.Application.Tests.DependencyInjection;

public class ApplicationServiceCollectionExtensionsTests
{
    private static IServiceCollection BuildServicesWithMockedAbstractions()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IShortUrlRepository>());
        services.AddSingleton(Mock.Of<IShortCodeGenerator>());
        services.AddSingleton(Mock.Of<IDateTimeProvider>());
        services.AddSingleton(Mock.Of<IDomainEventDispatcher>());
        return services;
    }

    [Fact]
    public void AddApplication_RegistersCreateShortUrlUseCase()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        services.Should().Contain(d => d.ServiceType == typeof(CreateShortUrlUseCase));
    }

    [Fact]
    public void AddApplication_RegistersRedirectUseCase()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        services.Should().Contain(d => d.ServiceType == typeof(RedirectUseCase));
    }

    [Fact]
    public void AddApplication_RegistersGetShortUrlUseCase()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        services.Should().Contain(d => d.ServiceType == typeof(GetShortUrlUseCase));
    }

    [Fact]
    public void AddApplication_RegistersDisableShortUrlUseCase()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        services.Should().Contain(d => d.ServiceType == typeof(DisableShortUrlUseCase));
    }

    [Fact]
    public void AddApplication_RegistersEnableShortUrlUseCase()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        services.Should().Contain(d => d.ServiceType == typeof(EnableShortUrlUseCase));
    }

    [Fact]
    public void AddApplication_RegistersUpdateExpirationUseCase()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        services.Should().Contain(d => d.ServiceType == typeof(UpdateExpirationUseCase));
    }

    [Fact]
    public void AddApplication_AfterMockingAbstractions_AllUseCasesResolveFromProvider()
    {
        var services = BuildServicesWithMockedAbstractions();
        services.AddApplication();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<CreateShortUrlUseCase>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<RedirectUseCase>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<GetShortUrlUseCase>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<DisableShortUrlUseCase>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<EnableShortUrlUseCase>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<UpdateExpirationUseCase>().Should().NotBeNull();
    }
}
