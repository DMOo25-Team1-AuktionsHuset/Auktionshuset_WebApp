using Auktionshuset.Infrastructure.Data;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.Json;

namespace Auktionshuset.Infrastructure.Messaging
{
    public class DbInboxStore(AHDBContext context) : IInboxStore
    {
        public async Task<InboxTypes> StoreNewMessageAsync(
            string consumerName,
            string eventType,
            string? transportMessageId,
            byte[] payload,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool hasEventId = Guid.TryParse(transportMessageId, out var eventId) && eventId != Guid.Empty;

            byte[] fingerprintInput = JsonSerializer.SerializeToUtf8Bytes(new
            {
                EventType = eventType,
                TransportMessageId = transportMessageId,
                Payload = payload
            });

            string fingerprint = Convert.ToHexString(SHA256.HashData(fingerprintInput));

            string messageKey = hasEventId ? eventId.ToString("D") : $"invalid:{fingerprint}";

            var inboxMessage = new InboxMessage
            {
                InboxId = Guid.NewGuid(),
                EventId = hasEventId ? eventId : null,
                TransportMessageId = transportMessageId,
                ConsumerName = consumerName,
                MessageKey = messageKey,
                EventType = eventType,
                Payload = payload,
                RecievedAtTime = DateTime.UtcNow,
                Error = hasEventId ? null : "MessageId missing or not valid"
            };

            int inserted = await InsertMessageAsync(inboxMessage, cancellationToken);

            if (inserted == 1)
            {
                return hasEventId ? InboxTypes.Stored : InboxTypes.Quarantined;
            }

            InboxMessage existingMessage = await context.InboxMessages
                .AsNoTracking()
                .SingleAsync(existingMessage =>
                    existingMessage.ConsumerName == consumerName
                    && existingMessage.MessageKey == messageKey,
                    cancellationToken);

            bool sameMessage = existingMessage.EventType == eventType
                && existingMessage.Payload.SequenceEqual(inboxMessage.Payload);

            if (sameMessage)
            {
                return InboxTypes.Duplicate;
            }

            // if same EvenId but different content, save conflict
            // seperately without overwriting the original
            var conflict = new InboxMessage
            {
                InboxId = Guid.NewGuid(),
                EventId = inboxMessage.EventId,
                TransportMessageId = transportMessageId,
                ConsumerName = consumerName,
                MessageKey = $"conflict:{fingerprint}",
                EventType = eventType,
                Payload = payload,
                Error = "Messagekey already received with different routingkey or payload"
            };

            await InsertMessageAsync(conflict, cancellationToken);

            return InboxTypes.Quarantined;
        }

        public Task<int> InsertMessageAsync(
            InboxMessage inboxMessage,
            CancellationToken cancellationToken)
        {
            bool hasEventId = inboxMessage.EventId.HasValue;
            Guid eventId = inboxMessage.EventId.GetValueOrDefault();
            bool isQuarantined = inboxMessage.Error is not null;
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
                    CASE WHEN {hasEventId} THEN {eventId} ELSE NULL END,
                    NULLIF({transportMessageId}, ''),
                    {inboxMessage.ConsumerName},
                    {inboxMessage.MessageKey},
                    {inboxMessage.EventType},
                    {inboxMessage.Payload},
                    CURRENT_TIMESTAMP,
                    NULL,
                    NULLIF({error}, ''),
                    CASE
                        WHEN {isQuarantined} THEN CURRENT_TIMESTAMP
                        ELSE NULL
                    END
                )
                ON CONFLICT ("ConsumerName", "MessageKey") 
                DO NOTHING
                """, cancellationToken);
        }
    }
}
