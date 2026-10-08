using System.Text.Json;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Auktionshuset.Infrastructure.Messaging
{
    internal sealed class OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessor> logger,
        EventContractRegistry registry) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessBatchAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Outbox batch failed, worker retries");
                }

                try
                {
                    await Task.Delay(OutboxPolicy.PollInterval, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async Task ProcessBatchAsync(CancellationToken cancellationToken)
        {
            await using (AsyncServiceScope scope = scopeFactory.CreateAsyncScope())
            {
                DbOutboxStore store = scope.ServiceProvider.GetRequiredService<DbOutboxStore>();
                await store.QuarantineExhaustedAsync(cancellationToken);
            }

            for (int index = 0; index < OutboxPolicy.MaxMessagesPerCycle; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await using AsyncServiceScope scope =
                    scopeFactory.CreateAsyncScope();

                DbOutboxStore store = scope.ServiceProvider
                    .GetRequiredService<DbOutboxStore>();

                OutboxMessage? message =
                    await store.TryClaimAsync(cancellationToken);

                if (message == null)
                {
                    return;
                }

                await ProcessMessageAsync(
                    scope.ServiceProvider,
                    store,
                    message,
                    cancellationToken);
            }
        }

        private async Task ProcessMessageAsync(
            IServiceProvider services,
            DbOutboxStore store,
            OutboxMessage message,
            CancellationToken cancellationToken)
        {
            Guid leaseToken = message.LeaseToken
                ?? throw new InvalidOperationException(
                    "A reserved outbox message is missing a lease token");

            try
            {
                EventContractRegistry.EventContractRecord contract = registry.ByName(message.EventType);

                IIntegrationEvent integrationEvent =
                    registry.Deserialize(
                        contract,
                        message.Payload);

                if (integrationEvent.EventId != message.OutboxId)
                {
                    throw new JsonException(
                        "EventId in payload does not match OutboxId");
                }

                IIntegrationEventPublisher publisher = services
                    .GetRequiredService<IIntegrationEventPublisher>();

                using var timeout =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken);

                timeout.CancelAfter(
                    TimeSpan.FromSeconds(
                        OutboxPolicy.PublishTimeoutSeconds));

                await publisher.PublishAsync(
                    integrationEvent,
                    timeout.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                bool permanentFailure =
                    ex is JsonException or NotSupportedException;

                bool updated = await store.FailAsync(
                    message,
                    leaseToken,
                    ex,
                    permanentFailure,
                    cancellationToken);

                if (!updated)
                {
                    logger.LogWarning(
                        $"Outbox {message.OutboxId}: lease lost: error status not changed");

                    return;
                }

                logger.LogError(
                    ex,
                    "Outbox {OutboxId} failed at first attempt {Attempt}. Quaratine: {Quarantine}",
                    message.OutboxId,
                    message.Attempts,
                    permanentFailure || message.Attempts >= OutboxPolicy.MaxAttempts);

                return;
            }

            bool completed = await store.CompleteAsync(
                message.OutboxId,
                leaseToken,
                cancellationToken);

            if (!completed)
            {
                logger.LogWarning(
                    $"Outbox {message.OutboxId}: publishing confirmed, but lease was no longer valid. Message can be sent again.");
            }
        }

    }
}
