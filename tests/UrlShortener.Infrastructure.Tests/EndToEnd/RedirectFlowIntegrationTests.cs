using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Application.Abstractions;
using UrlShortener.Application.ShortUrls.Redirect;
using UrlShortener.Domain.ShortUrls;
using UrlShortener.Domain.ShortUrls.Events;
using UrlShortener.Infrastructure.Events;
using UrlShortener.Infrastructure.Events.Handlers;
using UrlShortener.Infrastructure.Persistence;
using UrlShortener.Infrastructure.Persistence.Repositories;
using UrlShortener.Infrastructure.Time;
using UrlShortener.Infrastructure.Tests.TestSupport;

namespace UrlShortener.Infrastructure.Tests.EndToEnd;

public class RedirectFlowIntegrationTests
{
    private const string ValidCode = "abc1234";
    private const string ValidUrl = "https://example.com/path";
    private const string AnyUserAgent = "Mozilla/5.0";
    private const string AnyIpAddress = "203.0.113.7";

    private static ServiceProvider BuildProvider(SqliteInMemoryFixture fixture)
    {
        var services = new ServiceCollection();
        services.AddSingleton(fixture.Options);
        services.AddScoped<ApplicationDbContext>();
        services.AddScoped<IShortUrlRepository, EfShortUrlRepository>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IDomainEventHandler<ShortUrlClickedEvent>, ShortUrlClickedEventHandler>();
        services.AddScoped<RedirectUseCase>();
        return services.BuildServiceProvider();
    }

    private static async Task<Guid> SeedAsync(SqliteInMemoryFixture fixture)
    {
        var entity = ShortUrl.Create(
            ShortCode.Create(ValidCode),
            OriginalUrl.Create(ValidUrl));

        await using var ctx = fixture.CreateContext();
        ctx.ShortUrls.Add(entity);
        await ctx.SaveChangesAsync();
        return entity.Id;
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_IncrementsClickCountInDatabase()
    {
        using var fixture = new SqliteInMemoryFixture();
        var seededId = await SeedAsync(fixture);

        await using (var provider = BuildProvider(fixture))
        await using (var scope = provider.CreateAsyncScope())
        {
            var sut = scope.ServiceProvider.GetRequiredService<RedirectUseCase>();
            var result = await sut.ExecuteAsync(
                new RedirectRequest(ValidCode, AnyUserAgent, AnyIpAddress),
                CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var fetched = await ctx.ShortUrls.SingleAsync(s => s.Id == seededId);
            fetched.ClickCount.Should().Be(1);
        }
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_PersistsClickAuditRow()
    {
        using var fixture = new SqliteInMemoryFixture();
        await SeedAsync(fixture);

        await using (var provider = BuildProvider(fixture))
        await using (var scope = provider.CreateAsyncScope())
        {
            var sut = scope.ServiceProvider.GetRequiredService<RedirectUseCase>();
            await sut.ExecuteAsync(
                new RedirectRequest(ValidCode, AnyUserAgent, AnyIpAddress),
                CancellationToken.None);
        }

        await using (var ctx = fixture.CreateContext())
        {
            (await ctx.ClickAudits.CountAsync()).Should().Be(1);
        }
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_ClickAuditMatchesEventDetails()
    {
        using var fixture = new SqliteInMemoryFixture();
        var seededId = await SeedAsync(fixture);

        await using (var provider = BuildProvider(fixture))
        await using (var scope = provider.CreateAsyncScope())
        {
            var sut = scope.ServiceProvider.GetRequiredService<RedirectUseCase>();
            await sut.ExecuteAsync(
                new RedirectRequest(ValidCode, AnyUserAgent, AnyIpAddress),
                CancellationToken.None);
        }

        await using (var ctx = fixture.CreateContext())
        {
            var audit = await ctx.ClickAudits.SingleAsync();
            audit.ShortUrlId.Should().Be(seededId);
            audit.ShortCodeValue.Should().Be(ValidCode);
            audit.UserAgent.Should().Be(AnyUserAgent);
            audit.IpAddress.Should().Be(AnyIpAddress);
        }
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_DomainEventsClearedAfterDispatch()
    {
        using var fixture = new SqliteInMemoryFixture();
        await SeedAsync(fixture);

        await using var provider = BuildProvider(fixture);
        await using var scope = provider.CreateAsyncScope();

        var sut = scope.ServiceProvider.GetRequiredService<RedirectUseCase>();
        await sut.ExecuteAsync(
            new RedirectRequest(ValidCode, AnyUserAgent, AnyIpAddress),
            CancellationToken.None);

        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entity = await ctx.ShortUrls.SingleAsync();
        entity.DomainEvents.Should().BeEmpty();
    }
}
