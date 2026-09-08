using Auktionshuset.Application.EventHandling;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Auktionshuset.Infrastructure.Messaging
{
    public class RabbitMqIntegrationEventPublisher : IIntegrationEventPublisher
    {
        private readonly IConnection _connection;

        public RabbitMqIntegrationEventPublisher(IConnection connection)
        {
            _connection = connection;
        }

        public async Task PublishAsync<TEvent>(
            TEvent integrationEvent,
            CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent

        {
            await using var channel = await _connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

            const string exchangeName = "auktionshuset.events";

            await channel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            var json = JsonSerializer.Serialize(integrationEvent);
            var body = Encoding.UTF8.GetBytes(json);

            var routingKey = typeof(TEvent).Name;

            await channel.BasicPublishAsync(
                exchange: exchangeName,
                routingKey: routingKey,
                body: body,
                cancellationToken: cancellationToken);
        }
    }
}
