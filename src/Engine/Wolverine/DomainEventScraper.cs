using DomainEssentials.Core.Abstractions;
using Engine.Core.Events;
using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;
using Wolverine.Runtime;

namespace Engine.Wolverine;

public sealed class DomainEventScraper(
    IEventMapper eventMapper,
    IEnumerable<ITransactionalDomainEventHandler> handlers) : IDomainEventScraper
{
    private readonly ILookup<Type, ITransactionalDomainEventHandler> _handlers = handlers.ToLookup(x => x.EventType);

    public async Task ScrapeEvents(DbContext dbContext, MessageContext bus)
    {
        while (true)
        {
            var events = dbContext.ChangeTracker.Entries<IAggregateRoot>()
                .SelectMany(entry => entry.Entity.ClearDomainEvents())
                .ToArray();

            if (events.Length == 0)
                return;

            foreach (var domainEvent in events)
            {
                var integrationEvent = eventMapper.MapToIntegrationEvent(domainEvent);
                if (integrationEvent is not null)
                {
                    await bus.PublishAsync(integrationEvent);
                    continue;
                }

                var eventType = domainEvent.GetType();
                if (!_handlers.Contains(eventType))
                    throw new InvalidOperationException($"No transactional handler is registered for {eventType.FullName}.");

                foreach (var handler in _handlers[eventType])
                    await handler.HandleDomainEventAsync(domainEvent, dbContext);
            }
        }
    }
}
