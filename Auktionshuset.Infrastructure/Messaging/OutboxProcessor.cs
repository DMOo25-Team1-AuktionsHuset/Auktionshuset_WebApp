using System;
using System.Text.Json;
using Auktionshuset.Application.Admin.Lots.CreateLot;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ApplicationDbContext = Auktionshuset.Infrastructure.Data.DbContext;

namespace Auktionshuset.Infrastructure.Messaging
{
    internal sealed class OutboxProcessor : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OutboxProcessor> _logger;

        public OutboxProcessor(
            IServiceScopeFactory scopeFactory,
            ILogger<OutboxProcessor> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await ProcessBatchAsync(cancellationToken);
            }
        }

        private async Task ProcessBatchAsync(CancellationToken cancellationToken)
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();

            var messages = await dbContext.Set<OutboxMessage>()
                .Where(m => m.ProcessedAtTime == null)
                .OrderBy(m => m.OccuredAtTime)
                .Take(50)
                .ToListAsync(cancellationToken);

            foreach (var message in messages)
            {
                try
                {
                    var type = Type.GetType(message.EventType)
                        ?? throw new InvalidOperationException(
                            $"Ukendt eventtype: {message.EventType}");

                    var integrationEvent = (IIntegrationEvent)System.Text.Json.JsonSerializer.Deserialize(
                        message.Payload, type);

                    if (integrationEvent is null)
                    {
                        throw new JsonException("Eventet kunne ikke deserialiseres.");
                    }

                    // Rækken markeres som behandlet, hvis det er lykkedes at publicere eventet.
                    await PublishAsync (publisher, integrationEvent, cancellationToken);

                    message.ProcessedAtTime = DateTime.UtcNow;
                    message.Error = null;
                }
                catch (Exception exception)
                {
                    message.Attempts++;
                    message.Error = exception.ToString();

                    _logger.LogError(exception, "Fejl ved behandling af outbox-besked {OutboxId}.", message.OutboxId);
                    
                }
            }
            await dbContext.SaveChangesAsync(cancellationToken);
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

                _ => throw new NotSupportedException(
                    $"Outbox understøtter ikke {integrationEvent.GetType().Name}.")
            };
        }
    }
}
