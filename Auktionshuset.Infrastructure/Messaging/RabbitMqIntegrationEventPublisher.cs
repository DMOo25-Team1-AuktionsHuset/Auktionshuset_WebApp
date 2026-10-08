using Auktionshuset.Application.EventHandling;
using RabbitMQ.Client;
using System.Text.Json;

namespace Auktionshuset.Infrastructure.Messaging
{
    internal sealed class RabbitMqIntegrationEventPublisher(IConnection connection, EventContractRegistry registry) : IIntegrationEventPublisher
    {

        public async Task PublishAsync<TEvent>(
            TEvent message,
            CancellationToken cancellationToken)
            where TEvent : IIntegrationEvent

        {
            cancellationToken.ThrowIfCancellationRequested();

            EventContractRegistry.EventContractRecord contract = registry.ByType(message.GetType());

            string routingKey = contract.EventContractName;
            string json = registry.Serialize(message);
            string exchangeName = RabbitMqTopology.EventExchange;

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

            byte[] body = JsonSerializer.SerializeToUtf8Bytes(          //Fix det her? Med encoding utf8?
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
