using Auktionshuset.Application.EventHandling;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Auktionshuset.Infrastructure.Messaging
{
    internal sealed class RabbitMqIntegrationEventPublisher(IConnection connection, RabbitMqRoutingKeyResolver routingKeyResolver) : IIntegrationEventPublisher
    {
        /// <summary>
        /// Serializes the event and publishes it to the event exchange using the routing key
        /// resolved for its type.
        /// </summary>
        /// <typeparam name="TEvent">The type of integration event being published.</typeparam>
        /// <param name="message">The integration event to serialize and publish.</param>
        /// <returns>A task that completes once the message has been published to the channel.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no routing key is defined for <typeparamref name="TEvent"/>.
        /// </exception>
        /// <seealso cref="RabbitMqRoutingKeyResolver"/>
        public async Task PublishAsync<TEvent>(
            TEvent message,
            CancellationToken cancellationToken)
            where TEvent : IIntegrationEvent

        {
            //string routingKey = routingKeyResolver.Resolve<TEvent>();
            //await using IChannel channel = await connection.CreateChannelAsync(
            //    cancellationToken: cancellationToken);

            //const string exchangeName = "auktionshuset.events";

            //await channel.ExchangeDeclareAsync(
            //    exchange: exchangeName,
            //    type: ExchangeType.Topic,
            //    durable: true,
            //    autoDelete: false,
            //    cancellationToken: cancellationToken);

            //string json = JsonSerializer.Serialize(message);
            //byte[] body = Encoding.UTF8.GetBytes(json);

            //await channel.BasicPublishAsync(
            //    exchange: exchangeName,
            //    routingKey: routingKey,
            //    body: body,
            //    cancellationToken: cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            string routingKey =
                routingKeyResolver.Resolve<TEvent>();

            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true);

            await using IChannel channel =
                await connection.CreateChannelAsync(
                    channelOptions,
                    cancellationToken);

            await channel.ExchangeDeclareAsync(
                exchange: RabbitMqTopology.EventExchange,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            byte[] body = JsonSerializer.SerializeToUtf8Bytes(
                message,
                message.GetType());

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                MessageId = message.EventId.ToString("D"),
                Type = routingKey
            };

            await channel.BasicPublishAsync(
                exchange: RabbitMqTopology.EventExchange,
                routingKey: routingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
        }
    }
}
