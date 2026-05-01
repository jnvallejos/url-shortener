using UrlShortener.Domain.Common;

namespace UrlShortener.Application.Abstractions;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct);
}
