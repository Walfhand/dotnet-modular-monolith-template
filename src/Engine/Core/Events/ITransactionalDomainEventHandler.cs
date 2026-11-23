using DomainEssentials.Core.Events;
using Microsoft.EntityFrameworkCore;

namespace Engine.Core.Events;

public interface ITransactionalDomainEventHandler
{
    Type EventType { get; }
    Task HandleDomainEventAsync(IDomainEvent domainEvent, DbContext dbContext);
}
