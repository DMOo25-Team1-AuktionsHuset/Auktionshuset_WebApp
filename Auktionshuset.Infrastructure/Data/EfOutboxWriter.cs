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
            cancellationToken.ThrowIfCancellationRequested();

            if (integrationEvent.OccurredAt.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Integration-eventets OccurredAt skal være UTC.",
                    nameof(integrationEvent));
            }

            EventContractRegistry.EventContractRecord contract = eventContracts.ByType(integrationEvent.GetType());
            string payload = eventContracts.Serialize(integrationEvent);

            var outboxMessage = new OutboxMessage
            {
                OutboxId = integrationEvent.EventId,
                EventType = contract.EventContractName,
                Payload = payload,
                OccurredAtTime = integrationEvent.OccurredAt
            };

            dbContext.OutboxMessages.Add(outboxMessage);

            // Ingen savechanges her da den skal deles med ændringen af Lot. 
            return Task.CompletedTask;
        }
    }
}
