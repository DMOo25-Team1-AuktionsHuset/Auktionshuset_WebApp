using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;
using Auktionshuset.Application.Admin.Lots.DeleteLot;
using Microsoft.Extensions.DependencyInjection;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Application.Admin.Lots.UpdateLot;

namespace Auktionshuset.Infrastructure.Messaging.Consumers.Lot
{
    internal sealed class LotDeletedConsumer : BackgroundService
    {
        private readonly IConnection _connection;
        private readonly IServiceScopeFactory _scopeFactory;

        public LotDeletedConsumer(
            IConnection connection,
            IServiceScopeFactory scopeFactory)
        {
            _connection = connection;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            await using var channel =
                await _connection.CreateChannelAsync(
                    cancellationToken: stoppingToken);

            var exchangeName = RabbitMqTopology.EventExchange;
            var queueName = RabbitMqTopology.Queues.Admin;
            var routingKey = RabbitMqTopology.RoutingKeys.LotDeleted;

            await channel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: stoppingToken);

            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken);

            await channel.QueueBindAsync(
                queue: queueName,
                exchange: exchangeName,
                routingKey: routingKey,
                cancellationToken: stoppingToken);

            var consumer =
                new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (_, eventArgs) => {
                var body = eventArgs.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);

                var message =
                    JsonSerializer.Deserialize<LotDeletedIntegrationEvent>(json)
                    ?? throw new InvalidOperationException(
                        "Failed to deserialize LotDeletedIntegrationEvent");

                await using var scope = _scopeFactory.CreateAsyncScope();

                var handler = scope.ServiceProvider
                    .GetRequiredService<
                        IIntegrationEventHandler<LotDeletedIntegrationEvent>>();

                await handler.HandleAsync(message, stoppingToken);

                await channel.BasicAckAsync(
                    deliveryTag: eventArgs.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);
            };

            await channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            await Task.Delay(
                Timeout.Infinite,
                stoppingToken);
        }
    }
    
}
