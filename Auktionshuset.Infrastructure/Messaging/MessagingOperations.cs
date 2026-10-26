using Auktionshuset.Application.Abstraction;
using Auktionshuset.Infrastructure.Data;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace Auktionshuset.Infrastructure.Messaging
{
    internal class MessagingOperations(
        AHDBContext db,
        EventContractRegistry registry,
        MessagingRuntime runtime,
        TimeProvider clock)
        : IMessagingOperations
    {
        private static readonly Encoding StrictUtf8 =
    new UTF8Encoding(false, true);

        public async Task<MessagingStatus> ReadStatusAsync(
            CancellationToken cancellationToken)
        {
            List<QueueStatus> rows = await db.Database
                .SqlQuery<QueueStatus>($"""
                WITH messages AS (
                    SELECT
                        'outbox'::text AS "Pipeline",
                        "ProcessedAtTime",
                        "QuarantinedAtTime",
                        "NextAttemptAtTime",
                        "LeaseExpiresAtTime",
                        "Attempts",
                        {OutboxPolicy.MaxAttempts} AS "MaxAttempts"
                    FROM "OutboxMessages"

                    UNION ALL

                    SELECT
                        'inbox'::text AS "Pipeline",
                        "ProcessedAtTime",
                        "InboxQuarantinedAtTime" AS "QuarantinedAtTime",
                        "NextAttemptAtTime",
                        "LeaseExpiresAtTime",
                        "Attempts",
                        {InboxPolicy.MaxAttempts} AS "MaxAttempts"
                    FROM "InboxMessages"
                    WHERE "ConsumerName" = {RabbitMqTopology.Queues.Admin}
                ),
                classified AS (
                    SELECT *,
                        (
                            "ProcessedAtTime" IS NULL
                            AND "QuarantinedAtTime" IS NULL
                        ) AS "IsPending",
                        (
                            "ProcessedAtTime" IS NULL
                            AND "QuarantinedAtTime" IS NULL
                            AND "Attempts" < "MaxAttempts"
                            AND "NextAttemptAtTime" <= CURRENT_TIMESTAMP
                            AND (
                                "LeaseExpiresAtTime" IS NULL
                                OR "LeaseExpiresAtTime" <= CURRENT_TIMESTAMP
                            )
                        ) AS "IsReady"
                    FROM messages
                )
                SELECT
                    "Pipeline",
                    COUNT(*) FILTER (
                        WHERE "IsPending"
                    ) AS "Pending",
                    COUNT(*) FILTER (
                        WHERE "IsReady"
                    ) AS "Ready",
                    COUNT(*) FILTER (
                        WHERE "IsPending"
                        AND "LeaseExpiresAtTime" > CURRENT_TIMESTAMP
                    ) AS "Leased",
                    COUNT(*) FILTER (
                        WHERE "ProcessedAtTime" IS NULL
                        AND "QuarantinedAtTime" IS NOT NULL
                    ) AS "Quarantined",
                    COUNT(*) FILTER (
                        WHERE "ProcessedAtTime" IS NOT NULL
                    ) AS "Processed",
                    MIN("NextAttemptAtTime") FILTER (
                        WHERE "IsReady"
                    ) AS "OldestReadyAtTime"
                FROM classified
                GROUP BY "Pipeline"
                """)
                .ToListAsync(cancellationToken);

            QueueStatus[] queues =
            [
                rows.SingleOrDefault(x => x.Pipeline == "outbox")
                ?? new QueueStatus { Pipeline = "outbox" },

            rows.SingleOrDefault(x => x.Pipeline == "inbox")
                ?? new QueueStatus { Pipeline = "inbox" }
            ];

            return new MessagingStatus(
                clock.GetUtcNow().UtcDateTime,
                queues);
        }

        public async Task<IReadOnlyList<MessageInfo>> GetQuarantineAsync(
            string pipeline,
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            if (skip < 0 || take is < 1 or > 100)
                throw new ArgumentOutOfRangeException(nameof(take));

            return await Messages(pipeline)
                .Where(x =>
                    x.ProcessedAtTime == null
                    && x.QuarantinedAtTime != null)
                .OrderBy(x => x.QuarantinedAtTime)
                .ThenBy(x => x.MessageId)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public async Task<MessageDetails?> GetMessageAsync(
            string pipeline,
            Guid messageId,
            CancellationToken cancellationToken)
        {
            MessageInfo? message = await Messages(pipeline)
                .SingleOrDefaultAsync(
                    x => x.MessageId == messageId,
                    cancellationToken);

            if (message is null)
                return null;

            List<ReplayAuditInfo> audits = await db.MessageReplayAudits
                .AsNoTracking()
                .Where(x =>
                    x.Pipeline == pipeline
                    && x.MessageId == messageId)
                .OrderByDescending(x => x.RequestedAtTime)
                .ThenByDescending(x => x.ReplayId)
                .Take(20)
                .Select(x => new ReplayAuditInfo(
                    x.ReplayId,
                    x.ActorId,
                    x.Reason,
                    x.RequestedAtTime,
                    x.PreviousAttempts,
                    x.PreviousError))
                .ToListAsync(cancellationToken);

            return new MessageDetails(message, audits);
        }

        public async Task<ReplayResult> ReplayAsync(
            string pipeline,
            Guid messageId,
            DateTime expectedQuarantinedAtTime,
            string actorId,
            string reason,
            CancellationToken cancellationToken)
        {
            reason = reason?.Trim() ?? "";
            actorId = actorId?.Trim() ?? "";

            if (pipeline is not ("outbox" or "inbox"))
                return new(400, "Pipeline skal være outbox eller inbox.");

            if (reason.Length is < 1 or > 500)
                return new(400, "Reason skal indeholde 1-500 tegn.");

            if (actorId.Length is < 1 or > 200)
                return new(400, "ActorId er ugyldigt.");

            if (expectedQuarantinedAtTime.Kind != DateTimeKind.Utc)
                return new(400, "Quarantine-tidspunkt skal være UTC.");

            await using var transaction =
                await db.Database.BeginTransactionAsync(cancellationToken);

            FormattableString select = pipeline == "outbox"
                ? $"""
                SELECT
                    "OutboxId" AS "MessageId",
                    "OutboxId" AS "EventId",
                    "EventType",
                    "Payload" AS "JsonPayload",
                    NULL::bytea AS "RawPayload",
                    NULL::text AS "ConsumerName",
                    NULL::text AS "MessageKey",
                    NULL::text AS "TransportMessageId",
                    "Attempts",
                    "Error",
                    "ProcessedAtTime",
                    "QuarantinedAtTime",
                    "LeaseExpiresAtTime",
                    CURRENT_TIMESTAMP AS "DatabaseNow"
                FROM "OutboxMessages"
                WHERE "OutboxId" = {messageId}
                FOR UPDATE
                """
                : $"""
                SELECT
                    "InboxId" AS "MessageId",
                    "EventId",
                    "EventType",
                    NULL::text AS "JsonPayload",
                    "Payload" AS "RawPayload",
                    "ConsumerName",
                    "MessageKey",
                    "TransportMessageId",
                    "Attempts",
                    "Error",
                    "ProcessedAtTime",
                    "InboxQuarantinedAtTime" AS "QuarantinedAtTime",
                    "LeaseExpiresAtTime",
                    CURRENT_TIMESTAMP AS "DatabaseNow"
                FROM "InboxMessages"
                WHERE "InboxId" = {messageId}
                FOR UPDATE
                """;

            // Materialisér direkte, så EF ikke komponerer oven på FOR UPDATE.
            List<ReplayRow> rows = await db.Database
                .SqlQuery<ReplayRow>(select)
                .ToListAsync(cancellationToken);

            ReplayRow? row = rows.SingleOrDefault();

            if (row is null)
                return new(404, "Beskeden findes ikke.");

            if (row.ProcessedAtTime != null
                || row.QuarantinedAtTime == null
                || row.QuarantinedAtTime != expectedQuarantinedAtTime)
            {
                return new(409, "Beskedens tilstand er ændret. Hent den igen.");
            }

            if (row.LeaseExpiresAtTime > row.DatabaseNow)
                return new(409, "Beskeden har en aktiv lease.");

            string? validationError = ValidateReplay(row, pipeline);

            if (validationError != null)
                return new(422, validationError);

            FormattableString update = pipeline == "outbox"
                ? $"""
                UPDATE "OutboxMessages"
                SET
                    "Attempts" = 0,
                    "NextAttemptAtTime" = CURRENT_TIMESTAMP,
                    "QuarantinedAtTime" = NULL,
                    "Error" = NULL,
                    "LeaseToken" = NULL,
                    "LeaseExpiresAtTime" = NULL
                WHERE "OutboxId" = {messageId}
                    AND "ProcessedAtTime" IS NULL
                    AND "QuarantinedAtTime" = {expectedQuarantinedAtTime}
                    AND (
                        "LeaseExpiresAtTime" IS NULL
                        OR "LeaseExpiresAtTime" <= CURRENT_TIMESTAMP
                    )
                """
                : $"""
                UPDATE "InboxMessages"
                SET
                    "Attempts" = 0,
                    "NextAttemptAtTime" = CURRENT_TIMESTAMP,
                    "InboxQuarantinedAtTime" = NULL,
                    "Error" = NULL,
                    "LeaseToken" = NULL,
                    "LeaseExpiresAtTime" = NULL
                WHERE "InboxId" = {messageId}
                    AND "ProcessedAtTime" IS NULL
                    AND "InboxQuarantinedAtTime" = {expectedQuarantinedAtTime}
                    AND (
                        "LeaseExpiresAtTime" IS NULL
                        OR "LeaseExpiresAtTime" <= CURRENT_TIMESTAMP
                    )
                """;

            int affected = await db.Database.ExecuteSqlInterpolatedAsync(
                update,
                cancellationToken);

            if (affected != 1)
                return new(409, "Beskeden kunne ikke frigives til replay.");

            db.MessageReplayAudits.Add(new MessageReplayAudit
            {
                ReplayId = Guid.NewGuid(),
                Pipeline = pipeline,
                MessageId = messageId,
                EventId = row.EventId,
                ActorId = actorId,
                Reason = reason,
                RequestedAtTime = clock.GetUtcNow().UtcDateTime,
                PreviousAttempts = row.Attempts,
                PreviousError = row.Error
            });

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            runtime.Replayed(pipeline);

            return new(202, "Beskeden er frigivet til et nyt worker-forsøg.");
        }

        private string? ValidateReplay(ReplayRow row, string pipeline)
        {
            try
            {
                if (row.EventId is not Guid eventId || eventId == Guid.Empty)
                    return "Beskeden mangler et gyldigt EventId.";

                if (pipeline == "inbox")
                {
                    if (row.ConsumerName != RabbitMqTopology.Queues.Admin)
                        return "Inbox-beskeden tilhører en anden consumer.";

                    if (row.MessageKey != eventId.ToString("D"))
                        return "Inbox-identiteten er ugyldig eller en konflikt.";

                    if (!Guid.TryParse(
                            row.TransportMessageId,
                            out Guid transportId)
                        || transportId != eventId)
                    {
                        return "TransportMessageId matcher ikke EventId.";
                    }
                }

                string json = pipeline == "outbox"
                    ? row.JsonPayload
                        ?? throw new PermanentMessageException("Payload mangler.")
                    : StrictUtf8.GetString(
                        row.RawPayload
                        ?? throw new PermanentMessageException("Payload mangler."));

                var contract = registry.ByName(row.EventType);
                var integrationEvent = registry.Deserialize(contract, json);

                if (integrationEvent.EventId != eventId)
                    return "EventId i payload matcher ikke beskedens identitet.";

                if (pipeline == "outbox")
                {
                    // Publisheren serialiserer eventet igen.
                    // Afvis almindelig replay, hvis det ville ændre payload.
                    byte[] original = Encoding.UTF8.GetBytes(json);
                    byte[] republished = Encoding.UTF8.GetBytes(
                        registry.Serialize(integrationEvent));

                    if (!original.AsSpan().SequenceEqual(republished))
                    {
                        return "Den nuværende serializer ændrer payload. " +
                               "Beskeden kræver en særskilt vurdering af " +
                               "kontraktkompatibilitet før replay.";
                    }
                }

                return null;
            }
            catch (Exception exception) when (
                exception is PermanentMessageException
                    or JsonException
                    or DecoderFallbackException
                    or NotSupportedException)
            {
                return exception.Message;
            }
        }

        private IQueryable<MessageInfo> Messages(string pipeline)
        {
            if (pipeline == "outbox")
            {
                return db.OutboxMessages.AsNoTracking()
                    .Select(x => new MessageInfo
                    {
                        Pipeline = "outbox",
                        MessageId = x.OutboxId,
                        EventId = x.OutboxId,
                        EventType = x.EventType,
                        Attempts = x.Attempts,
                        Error = x.Error,
                        NextAttemptAtTime = x.NextAttemptAtTime,
                        QuarantinedAtTime = x.QuarantinedAtTime,
                        ProcessedAtTime = x.ProcessedAtTime,
                        LeaseExpiresAtTime = x.LeaseExpiresAtTime
                    });
            }

            if (pipeline == "inbox")
            {
                return db.InboxMessages.AsNoTracking()
                    .Select(x => new MessageInfo
                    {
                        Pipeline = "inbox",
                        MessageId = x.InboxId,
                        EventId = x.EventId,
                        EventType = x.EventType,
                        ConsumerName = x.ConsumerName,
                        MessageKey = x.MessageKey,
                        Attempts = x.Attempts,
                        Error = x.Error,
                        NextAttemptAtTime = x.NextAttemptAtTime,
                        QuarantinedAtTime = x.InboxQuarantinedAtTime,
                        ProcessedAtTime = x.ProcessedAtTime,
                        LeaseExpiresAtTime = x.LeaseExpiresAtTime
                    });
            }

            throw new ArgumentException("Ugyldig pipeline.", nameof(pipeline));
        }
    }
}

internal sealed class ReplayRow
{
    public Guid MessageId { get; init; }
    public Guid? EventId { get; init; }

    public string EventType { get; init; } = "";

    public string? JsonPayload { get; init; }
    public byte[]? RawPayload { get; init; }

    public string? ConsumerName { get; init; }
    public string? MessageKey { get; init; }
    public string? TransportMessageId { get; init; }

    public int Attempts { get; init; }
    public string? Error { get; init; }

    public DateTime? ProcessedAtTime { get; init; }
    public DateTime? QuarantinedAtTime { get; init; }
    public DateTime? LeaseExpiresAtTime { get; init; }

    public DateTime DatabaseNow { get; init; }
}
