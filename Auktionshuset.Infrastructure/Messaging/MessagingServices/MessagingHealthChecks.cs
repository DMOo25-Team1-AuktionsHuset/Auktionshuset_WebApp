using Auktionshuset.Application.Abstraction;
using Auktionshuset.Infrastructure.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Auktionshuset.Infrastructure.Messaging.MessagingServices
{
    internal sealed class DatabaseHealthChecks(IServiceScopeFactory scopeFactory) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellation = default)
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();

            return await db.Database.CanConnectAsync(cancellation)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Databasen er ikke tilgængelig.");
        }
    }

    internal sealed class MessagingHealthCheck(IServiceScopeFactory scopeFactory,
        MessagingRuntime runtime,
        IOptions<MessagingOperationsOptions> options,
        TimeProvider clock) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();

            if (!await db.Database.CanConnectAsync(cancellationToken))
                return HealthCheckResult.Unhealthy("Databasen er ikke tilgængelig.");

            var view = runtime.Read();
            var settings = options.Value;

            DateTime now = clock.GetUtcNow().UtcDateTime;

            bool startupGrace = (now - view.StartedAtTime).TotalSeconds < settings.StartupGraceSeconds;

            var problems = new List<string>();

            if (view.Status is null)
            {
                if (!startupGrace)
                    problems.Add("Der findes ingen statusmåling");
            }
            else
            {
                double sampleAge = (now - view.Status.SampledAtTime).TotalSeconds;

                if (sampleAge > settings.StatusSampleIntervalSeconds * 3)
                    problems.Add("Statusmålingen er for gammel");

                foreach (QueueStatus queue in view.Status.Queues)
                {
                    if (queue.Quarantined > 0)
                        problems.Add($"{queue.Pipeline} har karantæne beskeder");

                    if (queue.OldestReadyAtTime is DateTime oldest
                        && (now - oldest).TotalSeconds > settings.BacklogWarningAfterSeconds)
                        problems.Add($"{queue.Pipeline} har en gammel ready-backlog");
                }
            }

            if (view.SampleError != null) problems.Add("Seneste statusmåling fejlede");

            if (!startupGrace && settings.RunWorkers)
            {
                if (IsStale(view.OutboxDbActivity))
                    problems.Add("Outbox-workerens heartbeat mangler.");

                if (IsStale(view.InboxDbActivity))
                    problems.Add("Inbox-workerens heartbeat mangler.");

                if (!view.ConsumerActive)
                    problems.Add("RabbitMQ-consumeren er ikke aktiv.");
            }

            if (!startupGrace
                && settings.RunMaintenance
                && IsStale(view.MaintenanceDbActivity))
                problems.Add("Maintenance-workerens heartbeat mangler.");

            var data = new Dictionary<string, object>
            {
                ["instanceId"] = view.InstanceId,
                ["workersEnabled"] = view.WorkersEnabled,
                ["consumerActive"] = view.ConsumerActive,
                ["problems"] = problems.ToArray()
            };

            return problems.Count == 0
                ? HealthCheckResult.Healthy("Messaging fungerer.", data)
                : HealthCheckResult.Degraded(string.Join(" ", problems), data: data);

            bool IsStale(DateTime? lastActivity)
            {
                return (now - (lastActivity ?? view.StartedAtTime))
                    .TotalSeconds > settings.WorkerStaleAfterSeconds;
            }
        }
    }
}
