using Auktionshuset.Application.Abstraction.Admin.Auctions;
using Auktionshuset.Application.EventHandling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Text.Json;

namespace Auktionshuset.Infrastructure.Messaging.Consumers
{
    internal sealed class AuctionCreatedConsumer : BackgroundService
    {
        private readonly IConnection _connection;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly string _queueName;

        public AuctionCreatedConsumer(
            IConnection connection,
            IServiceScopeFactory scopeFactory)
        {
            _connection = connection;
            _scopeFactory = scopeFactory;

            // Each server instance gets its own queue, so every running instance
            // receives the event and can notify its own connected clients.
            _queueName = $"{RabbitMqTopology.Queues.AuctionCreated}.{Guid.NewGuid():N}";
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            await using var channel =
                await _connection.CreateChannelAsync(
                    cancellationToken: stoppingToken);

            var exchangeName = RabbitMqTopology.EventExchange;
            var routingKey = RabbitMqTopology.RoutingKeys.AuctionCreated;

            await channel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: stoppingToken);

            await channel.QueueDeclareAsync(
                queue: _queueName,
                durable: false,
                exclusive: true,
                autoDelete: true,
                cancellationToken: stoppingToken);

            await channel.QueueBindAsync(
                queue: _queueName,
                exchange: exchangeName,
                routingKey: routingKey,
                cancellationToken: stoppingToken);

            var consumer =
                new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (_, eventArgs) =>
            {
                var body = eventArgs.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);

                var message =
                    JsonSerializer.Deserialize<AuctionCreatedIntegrationEvent>(json)
                    ?? throw new InvalidOperationException(
                        "Failed to deserialize AuctionCreatedIntegrationEvent");

                await using var scope = _scopeFactory.CreateAsyncScope();

                var handler = scope.ServiceProvider
                    .GetRequiredService<
                        IIntegrationEventHandler<AuctionCreatedIntegrationEvent>>();

                await handler.HandleAsync(message, stoppingToken);

                await channel.BasicAckAsync(
                    deliveryTag: eventArgs.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);
            };

            await channel.BasicConsumeAsync(
                queue: _queueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            await Task.Delay(
                Timeout.Infinite,
                stoppingToken);
        }
    }
}
