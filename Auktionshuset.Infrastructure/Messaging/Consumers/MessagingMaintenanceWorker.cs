using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Messaging.MessagingServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Infrastructure.Messaging.Consumers
{
    internal class MessagingMaintenanceWorker(
        IServiceScopeFactory scopeFactory,
        MessagingRuntime runtime,
        IOptions<MessagingOperationsOptions> options,
        ILogger<MessagingMaintenanceWorker> logger)
        : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            MessagingOperationsOptions settings = options.Value;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();

                    var db = scope.ServiceProvider
                        .GetRequiredService<AHDBContext>();

                    int deleted = await db.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                    WITH expired AS (
                        SELECT "OutboxId"
                        FROM "OutboxMessages"
                        WHERE "ProcessedAtTime" <
                            CURRENT_TIMESTAMP
                            - make_interval(
                                days => {settings.OutboxRetentionDays})
                            AND "QuarantinedAtTime" IS NULL
                            AND "LeaseToken" IS NULL
                            AND (
                                "LeaseExpiresAtTime" IS NULL
                                OR "LeaseExpiresAtTime" <= CURRENT_TIMESTAMP
                            )
                        ORDER BY "ProcessedAtTime", "OutboxId"
                        LIMIT {settings.CleanupBatchSize}
                        FOR UPDATE SKIP LOCKED
                    )
                    DELETE FROM "OutboxMessages" AS message
                    USING expired
                    WHERE message."OutboxId" = expired."OutboxId"
                    """,
                        stoppingToken);

                    runtime.Touch("maintenance");
                    runtime.Cleaned(deleted);

                    if (deleted > 0)
                    {
                        logger.LogInformation(
                            "Slettede {Count} færdigbehandlede Outbox-beskeder.",
                            deleted);
                    }
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Outbox-oprydning fejlede. Forsøger igen senere.");
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(
                        settings.MaintenanceIntervalSeconds),
                    stoppingToken);
            }
        }
    }
}
