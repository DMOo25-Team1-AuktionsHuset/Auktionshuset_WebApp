using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Messaging;


namespace Auktionshuset.Infrastructure.Data
{
    internal sealed class EfOutboxWriter(
        AHDBContext dbContext,
        EventContractRegistry eventContracts) : IOutboxWriter
    {
        public Task AddAsync(
            IIntegrationEvent integrationEvent,
            CancellationToken cancellationToken = default)
        {
            EventContractRegistry.EventContractRecord contract = eventContracts.ByType(integrationEvent.GetType());
            string payload = eventContracts.Serialize(integrationEvent);

            var outboxMessage = new OutboxMessage
            {
                OutboxId = integrationEvent.EventId,
                EventType = contract.EventContractName,
                Payload = payload,
                OccuredAtTime = integrationEvent.OccurredAt.ToUniversalTime()
            };

            dbContext.OutboxMessages.Add(outboxMessage);

            // Ingen savechanges her da den skal deles med ændringen af Lot. 
            return Task.CompletedTask;
        }
    }
}
