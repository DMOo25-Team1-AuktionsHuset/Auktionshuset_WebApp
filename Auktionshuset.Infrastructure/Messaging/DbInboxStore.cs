using System.Security.Cryptography;
using System.Text.Json;
using Auktionshuset.Infrastructure.Data;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Infrastructure.Messaging
{
    public class DbInboxStore(AHDBContext context)
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
                ReceivedAtTime = DateTime.UtcNow,
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
                    "ReceivedAtTime",
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

        public async Task QuarantineExhaustedAsync(string consumerName, CancellationToken cancellationToken)
        {
            int maxAttempts = InboxPolicy.MaxAttempts;

            await context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "InboxMessages"
                SET
                    "InboxQuarantinedAtTime" = CURRENT_TIMESTAMP,
                    "LeaseToken" = NULL,
                    "LeaseExpiresAtTime" = NULL,
                    "Error" = COALESCE(
                        "Error",
                        'Last try did not stop before lease expired')
                WHERE "ConsumerName" = {consumerName}
                    AND "ProcessedAtTime" IS NULL
                    AND "InboxQuarantinedAtTime" IS NULL
                    AND "Attempts" >= {maxAttempts}
                    AND (
                        "LeaseExpiresAtTime" IS NULL
                        OR "LeaseExpiresAtTime" <= CURRENT_TIMESTAMP
                    )
                """, cancellationToken);
        }

        public async Task<InboxMessage?> TryClaimAsync(string consumerName, CancellationToken cancellation)
        {
            Guid leaseToken = Guid.NewGuid();
            int leaseSeconds = InboxPolicy.LeaseSeconds;
            int maxAttempts = InboxPolicy.MaxAttempts;

            List<InboxMessage> messages = await context.InboxMessages
                .FromSqlInterpolated($"""
                WITH unclaimed AS (
                    SELECT "InboxId"
                    FROM "InboxMessages"
                    WHERE "ConsumerName" = {consumerName}
                        AND "ProcessedAtTime" IS NULL
                        AND "InboxQuarantinedAtTime" IS NULL
                        AND "Attempts" < {maxAttempts}
                        AND "NextAttemptAtTime" <= CURRENT_TIMESTAMP
                        AND (
                            "LeaseExpiresAtTime" IS NULL
                            OR "LeaseExpiresAtTime" <= CURRENT_TIMESTAMP
                    )
                    ORDER BY
                        "NextAttemptAtTime",
                        "ReceivedAtTime",
                        "InboxId"
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                )
                UPDATE "InboxMessages" AS message
                SET
                    "LeaseToken" = {leaseToken},
                    "LeaseExpiresAtTime" = 
                        CURRENT_TIMESTAMP
                        + make_interval(secs => {leaseSeconds}),
                    "Attempts" = message."Attempts" + 1
                FROM unclaimed
                WHERE message."InboxId" = unclaimed."InboxId"
                RETURNING message.*
                """)
                .AsNoTracking()
                .ToListAsync(cancellation);

            return messages.SingleOrDefault();
        }

        public async Task<bool> CompleteAsync(
            Guid inboxId,
            Guid leaseToken,
            CancellationToken cancellationToken)
        {
            int affected = await context.Database
                .ExecuteSqlInterpolatedAsync($"""
                UPDATE "InboxMessages"
                SET
                    "ProcessedAtTime" = CURRENT_TIMESTAMP,
                    "Error" = NULL,
                    "LeaseToken" = NULL,
                    "LeaseExpiresAtTime" = NULL
                WHERE "InboxId" = {inboxId}
                    AND "LeaseToken" = {leaseToken}
                    AND "LeaseExpiresAtTime" > CURRENT_TIMESTAMP
                    AND "ProcessedAtTime" IS NULL
                    AND "InboxQuarantinedAtTime" IS NULL
                """, cancellationToken);

            return affected == 1;
        }

        public async Task<bool> FailAsync(
            InboxMessage message,
            Guid leaseToken,
            Exception exception,
            bool permanantFailure,
            CancellationToken cancellationToken)
        {
            bool quarantine = permanantFailure ||
                message.Attempts >= InboxPolicy.MaxAttempts;

            int retrySeconds =
                InboxPolicy.RetryDelaySeconds(message.Attempts);

            string error = exception.ToString();

            if (error.Length > 4000)
                error = error[..4000];

            int affected = await context.Database
                .ExecuteSqlInterpolatedAsync($"""
                UPDATE "InboxMessages"
                SET
                    "Error" = {error},
                    "NextAttemptAtTime" =
                        CURRENT_TIMESTAMP
                        + make_interval(secs => {retrySeconds}),
                    "InboxQuarantinedAtTime" =
                        CASE
                            WHEN {quarantine}
                            THEN CURRENT_TIMESTAMP
                            ELSE NULL
                        END,
                    "LeaseToken" = NULL,
                    "LeaseExpiresAtTime" = NULL
                WHERE "InboxId" = {message.InboxId}
                    AND "LeaseToken" = {leaseToken}
                    AND "LeaseExpiresAtTime" > CURRENT_TIMESTAMP
                    AND "ProcessedAtTime" IS NULL
                    AND "InboxQuarantinedAtTime" IS NULL
                """, cancellationToken);

            return affected == 1;
        }
    }
}
