using Auktionshuset.Infrastructure.Data;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Infrastructure.Messaging
{
    public class DbOutboxStore(AHDBContext context)
    {
        public async Task QuarantineExhaustedAsync(CancellationToken cancellationToken)
        {
            int maxAttempts = OutboxPolicy.MaxAttempts;

            await context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "OutboxMessages"
                SET
                    "QuarantinedAtTime" = CURRENT_TIMESTAMP,
                    "LeaseToken" = NULL,
                    "LeaseExpiresAtTime" = NULL,
                    "Error" = COALESCE(
                        "Error",
                        'Last try did not stop before lease expired')
                WHERE "ProcessedAtTime" IS NULL
                    AND "QuarantinedAtTime" IS NULL
                    AND "Attempts" >= {maxAttempts}
                    AND (
                        "LeaseExpiresAtTime" IS NULL
                        OR "LeaseExpiresAtTime" <= CURRENT_TIMESTAMP
                    )
                """, cancellationToken);
        }

        public async Task<OutboxMessage?> TryClaimAsync(
            CancellationToken cancellation)
        {
            Guid leaseToken = Guid.NewGuid();
            int leaseSeconds = OutboxPolicy.LeaseSeconds;
            int maxAttempts = OutboxPolicy.MaxAttempts;

            List<OutboxMessage> messages = await context.OutboxMessages
                .FromSqlInterpolated($"""
                WITH unclaimed AS (
                    SELECT "OutboxId"
                    FROM "OutboxMessages"
                    WHERE "ProcessedAtTime" IS NULL
                        AND "QuarantinedAtTime" IS NULL
                        AND "Attempts" < {maxAttempts}
                        AND "NextAttemptAtTime" <= CURRENT_TIMESTAMP
                        AND (
                            "LeaseExpiresAtTime" IS NULL
                            OR "LeaseExpiresAtTime" <= CURRENT_TIMESTAMP
                    )
                    ORDER BY
                        "NextAttemptAtTime",
                        "OccurredAtTime",
                        "OutboxId"
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                )
                UPDATE "OutboxMessages" AS message
                SET
                    "LeaseToken" = {leaseToken},
                    "LeaseExpiresAtTime" = 
                        CURRENT_TIMESTAMP
                        + make_interval(secs => {leaseSeconds}),
                    "Attempts" = message."Attempts" + 1
                FROM unclaimed
                WHERE message."OutboxId" = unclaimed."OutboxId"
                RETURNING message.*
                """)
                .AsNoTracking()
                .ToListAsync(cancellation);

            return messages.SingleOrDefault();
        }

        public async Task<bool> CompleteAsync(
            Guid outboxId,
            Guid leaseToken,
            CancellationToken cancellationToken)
        {
            int affected = await context.Database
                .ExecuteSqlInterpolatedAsync($"""
                UPDATE "OutboxMessages"
                SET
                    "ProcessedAtTime" = CURRENT_TIMESTAMP,
                    "Error" = NULL,
                    "LeaseToken" = NULL,
                    "LeaseExpiresAtTime" = NULL
                WHERE "OutboxId" = {outboxId}
                    AND "LeaseToken" = {leaseToken}
                    AND "LeaseExpiresAtTime" > CURRENT_TIMESTAMP
                    AND "ProcessedAtTime" IS NULL
                    AND "QuarantinedAtTime" IS NULL
                """, cancellationToken);

            return affected == 1;
        }

        public async Task<bool> FailAsync(
            OutboxMessage message,
            Guid leaseToken,
            Exception exception,
            bool permanantFailure,
            CancellationToken cancellationToken)
        {
            bool quarantine = permanantFailure ||
                message.Attempts >= OutboxPolicy.MaxAttempts;

            int retrySeconds =
                OutboxPolicy.RetryDelaySeconds(message.Attempts);

            string error = exception.ToString();

            if (error.Length > 4000)
                error = error[..4000];

            int affected = await context.Database
                .ExecuteSqlInterpolatedAsync($"""
                UPDATE "OutboxMessages"
                SET
                    "Error" = {error},
                    "NextAttemptAtTime" =
                        CURRENT_TIMESTAMP
                        + make_interval(secs => {retrySeconds}),
                    "QuarantinedAtTime" =
                        CASE
                            WHEN {quarantine}
                            THEN CURRENT_TIMESTAMP
                            ELSE NULL
                        END,
                    "LeaseToken" = NULL,
                    "LeaseExpiresAtTime" = NULL
                WHERE "OutboxId" = {message.OutboxId}
                    AND "LeaseToken" = {leaseToken}
                    AND "LeaseExpiresAtTime" > CURRENT_TIMESTAMP
                    AND "ProcessedAtTime" IS NULL
                    AND "QuarantinedAtTime" IS NULL
                """, cancellationToken);

            return affected == 1;
        }
    }
}
