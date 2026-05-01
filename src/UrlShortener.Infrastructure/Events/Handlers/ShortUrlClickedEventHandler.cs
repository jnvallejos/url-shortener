using UrlShortener.Domain.ClickAudits;
using UrlShortener.Domain.ShortUrls.Events;
using UrlShortener.Infrastructure.Persistence;

namespace UrlShortener.Infrastructure.Events.Handlers;

public sealed class ShortUrlClickedEventHandler : IDomainEventHandler<ShortUrlClickedEvent>
{
    private readonly ApplicationDbContext _db;

    public ShortUrlClickedEventHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task HandleAsync(ShortUrlClickedEvent @event, CancellationToken ct)
    {
        var audit = ClickAudit.Create(
            shortUrlId:     @event.ShortUrlId,
            shortCodeValue: @event.ShortCodeValue,
            clickedAt:      @event.ClickedAt,
            userAgent:      @event.UserAgent,
            ipAddress:      @event.IpAddress);

        _db.ClickAudits.Add(audit);
        await _db.SaveChangesAsync(ct);
    }
}
