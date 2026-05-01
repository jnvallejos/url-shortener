using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UrlShortener.Domain.Common;
using UrlShortener.Infrastructure.Events;

namespace UrlShortener.Infrastructure.Tests.Events;

public class DomainEventDispatcherTests
{
    public sealed record FooEvent(DateTime OccurredOn) : IDomainEvent;

    public sealed record BarEvent(DateTime OccurredOn) : IDomainEvent;

    private static IServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task DispatchAsync_WithNoEvents_DoesNotResolveHandlers()
    {
        var fooHandler = new Mock<IDomainEventHandler<FooEvent>>();
        var provider = BuildProvider(s => s.AddSingleton(fooHandler.Object));
        var sut = new DomainEventDispatcher(provider);

        await sut.DispatchAsync(Array.Empty<IDomainEvent>(), CancellationToken.None);

        fooHandler.Verify(
            h => h.HandleAsync(It.IsAny<FooEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WithSingleEvent_InvokesRegisteredHandler()
    {
        var fooHandler = new Mock<IDomainEventHandler<FooEvent>>();
        var provider = BuildProvider(s => s.AddSingleton(fooHandler.Object));
        var sut = new DomainEventDispatcher(provider);
        var fooEvent = new FooEvent(DateTime.UtcNow);

        await sut.DispatchAsync(new IDomainEvent[] { fooEvent }, CancellationToken.None);

        fooHandler.Verify(
            h => h.HandleAsync(fooEvent, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WithMultipleEventsOfSameType_InvokesHandlerForEach()
    {
        var fooHandler = new Mock<IDomainEventHandler<FooEvent>>();
        var provider = BuildProvider(s => s.AddSingleton(fooHandler.Object));
        var sut = new DomainEventDispatcher(provider);
        var first = new FooEvent(DateTime.UtcNow);
        var second = new FooEvent(DateTime.UtcNow.AddSeconds(1));

        await sut.DispatchAsync(new IDomainEvent[] { first, second }, CancellationToken.None);

        fooHandler.Verify(
            h => h.HandleAsync(It.IsAny<FooEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task DispatchAsync_WithMultipleHandlersForSameEvent_InvokesAllOfThem()
    {
        var firstHandler = new Mock<IDomainEventHandler<FooEvent>>();
        var secondHandler = new Mock<IDomainEventHandler<FooEvent>>();
        var provider = BuildProvider(s =>
        {
            s.AddSingleton(firstHandler.Object);
            s.AddSingleton(secondHandler.Object);
        });
        var sut = new DomainEventDispatcher(provider);
        var fooEvent = new FooEvent(DateTime.UtcNow);

        await sut.DispatchAsync(new IDomainEvent[] { fooEvent }, CancellationToken.None);

        firstHandler.Verify(
            h => h.HandleAsync(fooEvent, It.IsAny<CancellationToken>()),
            Times.Once);
        secondHandler.Verify(
            h => h.HandleAsync(fooEvent, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WithEventTypeWithoutHandler_DoesNotThrow()
    {
        var fooHandler = new Mock<IDomainEventHandler<FooEvent>>();
        var provider = BuildProvider(s => s.AddSingleton(fooHandler.Object));
        var sut = new DomainEventDispatcher(provider);
        var barEvent = new BarEvent(DateTime.UtcNow);

        Func<Task> act = () =>
            sut.DispatchAsync(new IDomainEvent[] { barEvent }, CancellationToken.None);

        await act.Should().NotThrowAsync();
        fooHandler.Verify(
            h => h.HandleAsync(It.IsAny<FooEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenHandlerThrows_PropagatesException()
    {
        var fooHandler = new Mock<IDomainEventHandler<FooEvent>>();
        fooHandler
            .Setup(h => h.HandleAsync(It.IsAny<FooEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var provider = BuildProvider(s => s.AddSingleton(fooHandler.Object));
        var sut = new DomainEventDispatcher(provider);
        var fooEvent = new FooEvent(DateTime.UtcNow);

        Func<Task> act = () =>
            sut.DispatchAsync(new IDomainEvent[] { fooEvent }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    [Fact]
    public async Task DispatchAsync_WhenCancellationRequested_PropagatesOperationCanceledException()
    {
        var fooHandler = new Mock<IDomainEventHandler<FooEvent>>();
        var provider = BuildProvider(s => s.AddSingleton(fooHandler.Object));
        var sut = new DomainEventDispatcher(provider);
        var fooEvent = new FooEvent(DateTime.UtcNow);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () =>
            sut.DispatchAsync(new IDomainEvent[] { fooEvent }, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
