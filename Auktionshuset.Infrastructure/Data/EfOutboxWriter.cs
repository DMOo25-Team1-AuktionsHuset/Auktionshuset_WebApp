using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Infrastructure.Data
{
    internal sealed class EfOutboxWriter(DbContext dbContext) : IOutboxWriter
    {
        private readonly DbContext _dbContext = dbContext;
        public Task AddAsync(
            IIntegrationEvent integrationEvent,
            CancellationToken cancellationToken = default)
        {
            OutboxMessage outboxMessage = new OutboxMessage
            {
                OutboxId = integrationEvent.EventId,
                EventType = integrationEvent.GetType().AssemblyQualifiedName !,
                Payload = JsonSerializer.Serialize(
                    integrationEvent,
                    integrationEvent.GetType()),
                OccuredAtTime = integrationEvent.OccurredAt.ToUniversalTime()
            };

             _dbContext.Set<OutboxMessage>().Add(outboxMessage);
            
             // Ingen savechanges her da den skal deles med ændringen af Lot. 
             return Task.CompletedTask;
        }
    }
}
