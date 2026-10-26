using System.Text;
using System.Text.Json;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Infrastructure.Data;
using Auktionshuset.Infrastructure.Messaging.MessagingServices;
using Auktionshuset.Infrastructure.Messaging.RabbitMqServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Auktionshuset.Infrastructure.Messaging.InboxServices
{
    internal sealed class InboxProcessor(
    IServiceScopeFactory scopeFactory,
    EventContractRegistry registry,
    ILogger<InboxProcessor> logger,
    MessagingRuntime runtime)
    : BackgroundService
    {
        private const string ConsumerName =
            RabbitMqTopology.Queues.Admin;

        private static readonly Encoding StrictUtf8 =
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true);

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessCycleAsync(stoppingToken);
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
                        "Inbox-cyklussen fejlede. Worker forsøger igen.");
                }

                try
                {
                    await Task.Delay(
                        InboxPolicy.PollInterval,
                        stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async Task ProcessCycleAsync(
            CancellationToken stoppingToken)
        {
            await using (AsyncServiceScope scope =
                scopeFactory.CreateAsyncScope())
            {
                var store = scope.ServiceProvider
                    .GetRequiredService<DbInboxStore>();

                await store.QuarantineExhaustedAsync(
                    ConsumerName,
                    stoppingToken);
            }

            for (int index = 0;
                 index < InboxPolicy.MaxMessagesPerCycle;
                 index++)
            {
                stoppingToken.ThrowIfCancellationRequested();

                await using AsyncServiceScope scope =
                    scopeFactory.CreateAsyncScope();

                var store = scope.ServiceProvider
                    .GetRequiredService<DbInboxStore>();

                InboxMessage? message = await store.TryClaimAsync(
                    ConsumerName,
                    stoppingToken);

                if (message is null)
                {
                    return;
                }

                await ProcessMessageAsync(
                    scope.ServiceProvider,
                    store,
                    message,
                    stoppingToken);
            }
        }

        private async Task ProcessMessageAsync(
            IServiceProvider services,
            DbInboxStore store,
            InboxMessage message,
            CancellationToken stoppingToken)
        {
            using IDisposable measurement = runtime.Measure("inbox");

            Guid leaseToken = message.LeaseToken
                ?? throw new InvalidOperationException(
                    "En reserveret Inbox-besked mangler lease-token.");

            try
            {
                if (message.EventId is not Guid eventId ||
                    eventId == Guid.Empty)
                {
                    throw new PermanentMessageException(
                        "Inbox-beskeden mangler et gyldigt EventId.");
                }

                if (!Guid.TryParse(
                        message.TransportMessageId,
                        out Guid transportEventId)
                    || transportEventId != eventId)
                {
                    throw new PermanentMessageException(
                        "TransportMessageId matcher ikke Inbox-eventets ID.");
                }

                EventContractRegistry.EventContractRecord contract =
                    registry.ByName(message.EventType);

                string json = StrictUtf8.GetString(message.Payload);

                IIntegrationEvent integrationEvent =
                    registry.Deserialize(contract, json);

                if (integrationEvent.EventId != eventId)
                {
                    throw new PermanentMessageException(
                        "EventId i payload matcher ikke transportens EventId.");
                }

                using var timeout =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        stoppingToken);

                timeout.CancelAfter(
                    TimeSpan.FromSeconds(
                        InboxPolicy.HandlerTimeoutSeconds));

                timeout.Token.ThrowIfCancellationRequested();

                await contract.Dispatch(
                    services,
                    integrationEvent,
                    timeout.Token);

                timeout.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                // Behold reservationen. Et nyt forsøg kan overtage
                // efter lease-udløb.
                throw;
            }
            catch (Exception exception)
            {
                bool permanentFailure =
                    exception is PermanentMessageException
                        or JsonException
                        or DecoderFallbackException
                        or NotSupportedException;

                bool updated = await store.FailAsync(
                    message,
                    leaseToken,
                    exception,
                    permanentFailure,
                    stoppingToken);

                runtime.LeaseLost("inbox");

                if (!updated)
                {
                    logger.LogWarning(
                        "Inbox {InboxId}: lease mistet. " +
                        "Fejlstatus blev ikke ændret.",
                        message.InboxId);

                    return;
                }

                runtime.Failed("inbox");

                logger.LogError(
                    exception,
                    "Inbox {InboxId} fejlede ved forsøg {Attempt}. " +
                    "Quarantine: {Quarantine}.",
                    message.InboxId,
                    message.Attempts,
                    permanentFailure ||
                        message.Attempts >= InboxPolicy.MaxAttempts);

                return;
            }

            bool completed = await store.CompleteAsync(
                message.InboxId,
                leaseToken,
                stoppingToken);

            if (completed)
            {
                runtime.Completed("inbox");
            }
            else
            {
                runtime.LeaseLost("inbox");

                logger.LogWarning(
                    "Inbox {InboxId}: handlers afsluttede, " +
                    "men lease var ikke længere gyldig. " +
                    "Eventet kan blive behandlet igen.",
                    message.InboxId);
            }
        }
    }
}
