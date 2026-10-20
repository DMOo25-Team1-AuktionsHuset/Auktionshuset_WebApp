using Auktionshuset.Infrastructure.Data;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Infrastructure.Messaging
{
    public class DbInboxStore(AHDBContext context)
    {
        public async Task<InboxTypes> StoreNewMessageAsync(string consumerName,
            string eventType,
            string? transportMessageId,
            byte[] payload,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool hasEventId = Guid.TryParse(transportMessageId, out var eventId) && eventId != Guid.Empty;

            var inboxMessage = new InboxMessage
            {
                InboxId = Guid.NewGuid(),
                EventId = hasEventId ? eventId : null,
                TransportMessageId = transportMessageId,
                ConsumerName = consumerName,
                MessageKey = transportMessageId ?? string.Empty,
                EventType = eventType,
                Payload = payload,
                RecievedAtTime = DateTime.UtcNow
            };
            int inserted = await InsertMessageAsync(inboxMessage, cancellationToken);

            if (inserted == 1)
            {
                return hasEventId ? InboxTypes.Stored : InboxTypes.Quarantined;
            }

            InboxMessage? existingMessage = await context.InboxMessages
                .Where(m => m.ConsumerName == consumerName && m.MessageKey == inboxMessage.MessageKey)
                .FirstOrDefaultAsync(cancellationToken);

            bool sameMessage = existingMessage is not null
                && existingMessage.EventType == inboxMessage.EventType
                && existingMessage.Payload.SequenceEqual(inboxMessage.Payload);

            if (sameMessage)
            {
                return InboxTypes.Duplicate;
            }

            await InsertMessageAsync(inboxMessage, cancellationToken);

            return InboxTypes.Quarantined;
        }

        public Task<int> InsertMessageAsync(InboxMessage inboxMessage,
            CancellationToken cancellationToken)
        {
            bool hasEventId = inboxMessage.EventId.HasValue;
            Guid eventId = inboxMessage.EventId.GetValueOrDefault();
            bool isQuarantined = inboxMessage.Error is null;
            string transportMessageId = inboxMessage.TransportMessageId ?? string.Empty;
            string error = inboxMessage.Error ?? string.Empty;

            return context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "InboxMessages" (
                    "InboxId",
                    "EventId",
                    "TransportMessageId",
                    "ConsumerName",
                    "MessageKey",
                    "EventType",
                    "Payload",
                    "RecievedAtTime",
                    "ProcessedAtTime",
                    "Error",
                    "InboxQuarantinedAtTime"
                )
                VALUES (
                    {inboxMessage.InboxId},
                    {(hasEventId ? eventId : null)},
                    {transportMessageId},
                    {inboxMessage.ConsumerName},
                    {inboxMessage.MessageKey},
                    {inboxMessage.EventType},
                    {inboxMessage.Payload},
                    {inboxMessage.RecievedAtTime},
                    {(isQuarantined ? null : inboxMessage.ProcessedAtTime)},
                    {(isQuarantined ? null : error)},
                    {(isQuarantined ? inboxMessage.InboxQuarantinedAtTime : null)}
                )
                ON CONFLICT ("ConsumerName", "MessageKey") DO NOTHING
                """, cancellationToken);
        }
    }
}
