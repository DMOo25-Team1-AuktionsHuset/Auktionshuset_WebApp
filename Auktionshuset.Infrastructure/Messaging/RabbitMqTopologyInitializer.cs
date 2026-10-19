using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Infrastructure.Messaging
{
    internal class RabbitMqTopologyInitializer(EventContractRegistry registry)
    {
        public async Task EnsureAsync(IChannel channel, CancellationToken cancellationToken)
        {
            await channel.ExchangeDeclareAsync(
                exchange: RabbitMqTopology.EventExchange,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(
                queue: RabbitMqTopology.Queues.Admin,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: cancellationToken);

            foreach(string routingKey in registry.EventContractNames)
            {
                await channel.QueueBindAsync(
                    queue: RabbitMqTopology.Queues.Admin,
                    exchange: RabbitMqTopology.EventExchange,
                    routingKey: routingKey,
                    cancellationToken: cancellationToken);
            }
        }
    }
}
