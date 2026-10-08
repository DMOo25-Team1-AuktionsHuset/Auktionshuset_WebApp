using System.Text.Json;
using Auktionshuset.Application.Admin.Auctions.Bids;
using Auktionshuset.Application.Admin.Auctions.CreateAuction;
using Auktionshuset.Application.Admin.Auctions.UpdateAuction;
using Auktionshuset.Application.Admin.Auctions.DeleteAuction;
using Auktionshuset.Application.Admin.Employees.CreateEmployee;
using Auktionshuset.Application.Admin.Employees.UpdateEmployee;
using Auktionshuset.Application.Admin.Employees.DeleteEmployee;
using Auktionshuset.Application.Admin.Lots.CreateLot;
using Auktionshuset.Application.Admin.Lots.UpdateLot;
using Auktionshuset.Application.Admin.Lots.DeleteLot;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Infrastructure.Data;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Auktionshuset.Infrastructure.Messaging
{
    internal sealed class OutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessor> logger) : BackgroundService
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
                catch(Exception ex)
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
                var store = scope.ServiceProvider.GetRequiredService<DbOutboxStore>();
                await store.QuarantineExhaustedAsync(cancellationToken);
            }

            for (int index = 0; index < OutboxPolicy.MaxMessagesPerCycle; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await using AsyncServiceScope scope =
                    scopeFactory.CreateAsyncScope();

                var store = scope.ServiceProvider
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
                IIntegrationEvent integrationEvent =
                    Deserialize(message);

                var publisher = services
                    .GetRequiredService<IIntegrationEventPublisher>();

                using var timeout =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken);

                timeout.CancelAfter(
                    TimeSpan.FromSeconds(
                        OutboxPolicy.PublishTimeoutSeconds));

                await PublishAsync(
                    publisher,
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
                    $"Outbox {message.OutboxId}: publishing confirmed, " +
                    "but lease was no longer valid. " +
                    "Message can be sent again.");
            }
        }

        private static IIntegrationEvent Deserialize(OutboxMessage message)
        {
            Type? eventType = Type.GetType(message.EventType);

            if (eventType == null || !typeof(IIntegrationEvent).IsAssignableFrom(eventType)) {
                throw new NotSupportedException(
                    $"Unknown integration event type: {message.EventType}");
            }

            if (JsonSerializer.Deserialize(message.Payload, eventType) is not IIntegrationEvent integrationEvent)
            {
                throw new JsonException(
                    "Payload could not be read as a integration event");
            }

            if (integrationEvent.EventId != message.OutboxId)
            {
                throw new JsonException(
                    "EventId in payload does not match OutboxId");
            }

            return integrationEvent;
        }

        private static Task PublishAsync(
            IIntegrationEventPublisher publisher,
            IIntegrationEvent integrationEvent,
            CancellationToken cancellationToken)
        {
            //håndter kun Lotcreated som test
            return integrationEvent switch
            {
                LotCreatedIntegrationEvent lotCreated =>
                    publisher.PublishAsync(lotCreated, cancellationToken),

                LotUpdatedIntegrationEvent lotUpdated =>
                    publisher.PublishAsync(lotUpdated, cancellationToken),

                LotDeletedIntegrationEvent lotDeleted =>
                    publisher.PublishAsync(lotDeleted, cancellationToken),

                AuctionCreatedIntegrationEvent auctionCreated =>
                    publisher.PublishAsync(auctionCreated, cancellationToken),

                AuctionUpdatedIntegrationEvent auctionUpdated =>
                    publisher.PublishAsync(auctionUpdated, cancellationToken),

                AuctionDeletedIntegrationEvent auctionDeleted =>
                    publisher.PublishAsync(auctionDeleted, cancellationToken),

                EmployeeCreatedIntegrationEvent employeeCreated =>
                    publisher.PublishAsync(employeeCreated, cancellationToken),

                EmployeeUpdatedIntegrationEvent employeeUpdated =>
                    publisher.PublishAsync(employeeUpdated, cancellationToken),

                EmployeeDeletedIntegrationEvent employeeDeleted =>
                    publisher.PublishAsync(employeeDeleted, cancellationToken),

                BidPlacedIntegrationEvent bidPlaced =>
                    publisher.PublishAsync(bidPlaced, cancellationToken),

                AuctionCreatedIntegrationEvent auctionCreated =>
                    publisher.PublishAsync(auctionCreated, cancellationToken),

                AuctionUpdatedIntegrationEvent auctionUpdated =>
                    publisher.PublishAsync(auctionUpdated, cancellationToken),

                AuctionDeletedIntegrationEvent auctionDeleted =>
                    publisher.PublishAsync(auctionDeleted, cancellationToken),

                EmployeeCreatedIntegrationEvent employeeCreated =>
                    publisher.PublishAsync(employeeCreated, cancellationToken),

                EmployeeUpdatedIntegrationEvent employeeUpdated =>
                    publisher.PublishAsync(employeeUpdated, cancellationToken),

                EmployeeDeletedIntegrationEvent employeeDeleted =>
                    publisher.PublishAsync(employeeDeleted, cancellationToken),

                _ => throw new NotSupportedException(
                    $"Outbox understøtter ikke {integrationEvent.GetType().Name}.")
            };
        }
    }
}
