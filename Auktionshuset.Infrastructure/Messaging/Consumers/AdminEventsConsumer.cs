using Auktionshuset.Application.Admin.Auctions.CreateAuction;
using Auktionshuset.Application.Admin.Auctions.UpdateAuction;
using Auktionshuset.Application.Admin.Auctions.DeleteAuction;
using Auktionshuset.Application.Admin.Lots.CreateLot;
using Auktionshuset.Application.Admin.Lots.DeleteLot;
using Auktionshuset.Application.Admin.Lots.UpdateLot;
using Auktionshuset.Application.EventHandling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using Auktionshuset.Application.Admin.Employees.CreateEmployee;
using Auktionshuset.Application.Admin.Employees.UpdateEmployee;
using Auktionshuset.Application.Admin.Employees.DeleteEmployee;
using System.Reflection.Metadata;

namespace Auktionshuset.Infrastructure.Messaging.Consumers;

internal sealed class AdminEventsConsumer : BackgroundService
{
    private const string QueueName = RabbitMqTopology.Queues.Admin;

    private readonly IConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;

    public AdminEventsConsumer(
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

        foreach (var routingKey in new[]
        {
            RabbitMqTopology.RoutingKeys.LotCreated,
            RabbitMqTopology.RoutingKeys.LotUpdated,
            RabbitMqTopology.RoutingKeys.LotDeleted,
            RabbitMqTopology.RoutingKeys.AuctionCreated,
            RabbitMqTopology.RoutingKeys.EmployeeCreated,
            RabbitMqTopology.RoutingKeys.EmployeeUpdated,
            RabbitMqTopology.RoutingKeys.EmployeeDeleted,
            RabbitMqTopology.RoutingKeys.AuctionCreated,
            RabbitMqTopology.RoutingKeys.AuctionUpdated,
            RabbitMqTopology.RoutingKeys.AuctionDeleted
        })
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
            var json = Encoding.UTF8.GetString(eventArgs.Body.ToArray());

            switch (eventArgs.RoutingKey)
            {
                case var routingKey
                    when routingKey == RabbitMqTopology.RoutingKeys.LotCreated:
                    await HandleAsync<LotCreatedIntegrationEvent>(
                        json, stoppingToken);
                    break;

                case var routingKey
                    when routingKey == RabbitMqTopology.RoutingKeys.LotUpdated:
                    await HandleAsync<LotUpdatedIntegrationEvent>(
                        json, stoppingToken);
                    break;

                case var routingKey
                    when routingKey == RabbitMqTopology.RoutingKeys.LotDeleted:
                    await HandleAsync<LotDeletedIntegrationEvent>(
                        json, stoppingToken);
                    break;

                case var routingKey
                    when routingKey == RabbitMqTopology.RoutingKeys.AuctionCreated:
                    await HandleAsync<AuctionCreatedIntegrationEvent>(
                        json, stoppingToken);
                    break;

                case var routingKey
                    when routingKey == RabbitMqTopology.RoutingKeys.EmployeeCreated:
                    await HandleAsync<EmployeeCreatedIntegrationEvent>(
                        json, stoppingToken);
                    break;

                case var routingKey
                    when routingKey == RabbitMqTopology.RoutingKeys.EmployeeDeleted:
                    await HandleAsync<EmployeeDeletedIntegrationEvent>(
                        json, stoppingToken); 
                    break;

                case var routingKey
                    when routingKey == RabbitMqTopology.RoutingKeys.EmployeeUpdated:
                    await HandleAsync<EmployeeUpdatedIntegrationEvent>(
                        json, stoppingToken);
                    break;

                case var routingKey
                    when routingKey == RabbitMqTopology.RoutingKeys.AuctionUpdated:
                    await HandleAsync<AuctionUpdatedIntegrationEvent>(
                        json,
                        stoppingToken);
                    break;

                case var routingKey
                    when routingKey == RabbitMqTopology.RoutingKeys.AuctionDeleted:
                    await HandleAsync<AuctionDeletedIntegrationEvent>(
                        json,
                        stoppingToken);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"No handler mapping exists for routing key '{eventArgs.RoutingKey}'.");
            }

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

    private async Task HandleAsync<TEvent>(
        string json,
        CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        var message = JsonSerializer.Deserialize<TEvent>(json)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize {typeof(TEvent).Name}");

        await using var scope = _scopeFactory.CreateAsyncScope();

        var handler = scope.ServiceProvider
            .GetRequiredService<IIntegrationEventHandler<TEvent>>();

        await handler.HandleAsync(message, cancellationToken);
    }
}
