using UrlShortener.Domain.Common;

namespace UrlShortener.Infrastructure.Events;

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct);
}
