using Auktionshuset.Application.EventHandling;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Auktionshuset.Infrastructure.Messaging
{
    internal sealed class RabbitMqIntegrationEventPublisher : IIntegrationEventPublisher
    {
        private readonly IConnection _connection;
        private readonly RabbitMqRoutingKeyResolver _routingKeyResolver;

        /// <summary>
        /// Initializes a publisher that writes integration events to RabbitMQ.
        /// </summary>
        /// <param name="connection">The open RabbitMQ connection used to create publishing channels.</param>
        /// <param name="routingKeyResolver">The resolver that maps event types to routing keys.</param>
        public RabbitMqIntegrationEventPublisher(
            IConnection connection,
            RabbitMqRoutingKeyResolver routingKeyResolver)
        {
            _connection = connection;
            _routingKeyResolver = routingKeyResolver;
        }

        /// <summary>
        /// Serializes the event and publishes it to the event exchange using the routing key
        /// resolved for its type.
        /// </summary>
        /// <typeparam name="TEvent">The type of integration event being published.</typeparam>
        /// <param name="integrationEvent">The integration event to serialize and publish.</param>
        /// <returns>A task that completes once the message has been published to the channel.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no routing key is defined for <typeparamref name="TEvent"/>.
        /// </exception>
        /// <seealso cref="RabbitMqRoutingKeyResolver"/>
        public async Task PublishAsync<TEvent>(
            TEvent integrationEvent,
            CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent

        {
            var routingKey = _routingKeyResolver.Resolve<TEvent>();
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

            await channel.BasicPublishAsync(
                exchange: exchangeName,
                routingKey: routingKey,
                body: body,
                cancellationToken: cancellationToken);
        }
    }
}
