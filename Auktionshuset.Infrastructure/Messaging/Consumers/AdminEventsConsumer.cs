using Auktionshuset.Application.EventHandling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace Auktionshuset.Infrastructure.Messaging.Consumers;

internal sealed class AdminEventsConsumer(
    IConnection connection,
    IServiceScopeFactory scopeFactory,
    EventContractRegistry registry) : BackgroundService
{
    private const string QueueName = RabbitMqTopology.Queues.Admin;

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await using IChannel channel =
            await connection.CreateChannelAsync(
                cancellationToken: stoppingToken);

        string exchangeName = RabbitMqTopology.EventExchange;

        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        foreach (string routingKey in registry.EventContractNames)
        {
            await channel.QueueBindAsync(
                queue: QueueName,
                exchange: exchangeName,
                routingKey: routingKey,
                cancellationToken: stoppingToken);
        }

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            string json = Encoding.UTF8.GetString(
                eventArgs.Body.ToArray());

            EventContractRegistry.EventContractRecord contract = registry.ByName(eventArgs.RoutingKey);

            IIntegrationEvent message = registry.Deserialize(contract, json);

            await using AsyncServiceScope scope =
                scopeFactory.CreateAsyncScope();

            await contract.Dispatch(
                 scope.ServiceProvider,
                 message,
                 stoppingToken);

            await channel.BasicAckAsync(
                 deliveryTag: eventArgs.DeliveryTag,
                 multiple: false,
                 cancellationToken: stoppingToken);
        };

        await channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(
            Timeout.Infinite,
            stoppingToken);
    }

}
