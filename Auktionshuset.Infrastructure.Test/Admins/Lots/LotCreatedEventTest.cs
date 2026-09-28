using Auktionshuset.Application.EventHandling;
using Microsoft.Extensions.DependencyInjection;
using Auktionshuset.Infrastructure.Messaging.Consumers;
using RabbitMQ.Client;
using Moq;
using System.Text;
using System.Text.Json;
using Auktionshuset.Application.Admin.Lots.CreateLot;

namespace Auktionshuset.Infrastructure.Test.Admins.Lots
{
    public class LotCreatedEventTest
    {
        /// <summary>
        /// Round-trips a lot-created event through RabbitMQ to verify the exchange, binding and
        /// routing key. Requires a local RabbitMQ broker.
        /// </summary>
        [Fact]
        [Trait("Category", "Integration")]
        public async Task LotCreatedEvent_CanBePublishedToRabbitMq()
        {
            // Arrange 
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest"
            };

            await using IConnection connection =
                await factory.CreateConnectionAsync();

            await using IChannel channel =
                await connection.CreateChannelAsync();

            const string exchange = "auktionshuset.events";
            const string queue = "test.lot-created";
            const string routingKey = "lot.created.v1";

            await channel.ExchangeDeclareAsync(
                exchange,
                ExchangeType.Topic,
                durable: true,
                autoDelete: false);

            await channel.QueueDeclareAsync(
                queue,
                durable: false,
                exclusive: true,
                autoDelete: true);

            await channel.QueueBindAsync(
                queue,
                exchange,
                routingKey);

            // Act
            var eventMessage = new LotCreatedIntegrationEvent(
                EventId: Guid.NewGuid(),
                LotId: Guid.NewGuid(),
                AuctionHouseId: Guid.NewGuid(),
                Name: "Test Lot",
                Category: "Test Category",
                Quantity: 1,
                EstimatedValue: 100,
                OccurredAt: DateTime.UtcNow);

            string json = JsonSerializer.Serialize(eventMessage);
            byte[] body = Encoding.UTF8.GetBytes(json);

            await channel.BasicPublishAsync(
                exchange,
                routingKey,
                body);

            BasicGetResult? result = await channel.BasicGetAsync(
                queue,
                autoAck: true);

            // Assert
            Assert.NotNull(result);

            string receivedJson =
                Encoding.UTF8.GetString(result!.Body.ToArray());

            LotCreatedIntegrationEvent? receivedEvent =
                JsonSerializer.Deserialize<LotCreatedIntegrationEvent>(
                    receivedJson);

            Assert.NotNull(receivedEvent);
            Assert.Equal(eventMessage.LotId, receivedEvent!.LotId);
            Assert.Equal(eventMessage.Name, receivedEvent.Name);
            Assert.Equal("lot.created.v1", result.RoutingKey);
        }

        /// <summary>
        /// Verifies that the consumer deserializes a published lot-created event and forwards it to
        /// the registered handler. Requires a local RabbitMQ broker.
        /// </summary>
        [Fact]
        [Trait("Category", "Integration")]
        public async Task AdminConsumer_CallsLotCreatedHandler_WhenLotCreatedEventIsReceived()
        {
            var receivedEvent =
                new TaskCompletionSource<LotCreatedIntegrationEvent>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            var handlerMock =
                new Mock<IIntegrationEventHandler<LotCreatedIntegrationEvent>>();

            handlerMock
                .Setup(handler => handler.HandleAsync(
                    It.IsAny<LotCreatedIntegrationEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<LotCreatedIntegrationEvent, CancellationToken>(
                    (message, _) => receivedEvent.TrySetResult(message))
                .Returns(Task.CompletedTask);

            var services = new ServiceCollection();

            services.AddScoped<
                IIntegrationEventHandler<LotCreatedIntegrationEvent>>(
                _ => handlerMock.Object);

            await using ServiceProvider serviceProvider =
                services.BuildServiceProvider();

            IServiceScopeFactory scopeFactory =
                serviceProvider.GetRequiredService<IServiceScopeFactory>();

            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest"
            };

            await using IConnection connection =
                await factory.CreateConnectionAsync();

            await using IChannel publishChannel =
                await connection.CreateChannelAsync();

            const string exchangeName = "auktionshuset.events";
            const string queueName = "auktionshuset.admin";
            const string routingKey = "lot.created.v1";

            await publishChannel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false);

            await publishChannel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false);

            await publishChannel.QueueBindAsync(
                queue: queueName,
                exchange: exchangeName,
                routingKey: routingKey);

            await publishChannel.QueuePurgeAsync(queueName);

            using var consumer =
                new AdminEventsConsumer(connection, scopeFactory);

            using var cancellationSource =
                new CancellationTokenSource(TimeSpan.FromSeconds(10));

            await consumer.StartAsync(cancellationSource.Token);

            var expectedEvent = new LotCreatedIntegrationEvent(
                EventId: Guid.NewGuid(),
                LotId: Guid.NewGuid(),
                AuctionHouseId: Guid.NewGuid(),
                Name: "Test lot",
                Category: "Test kategori",
                Quantity: 2,
                EstimatedValue: 1500,
                OccurredAt: DateTime.UtcNow);

            string json = JsonSerializer.Serialize(expectedEvent);
            byte[] body = Encoding.UTF8.GetBytes(json);

            await publishChannel.BasicPublishAsync(
                exchange: exchangeName,
                routingKey: routingKey,
                body: body);

            LotCreatedIntegrationEvent actualEvent =
                await receivedEvent.Task.WaitAsync(
                    TimeSpan.FromSeconds(5));

            Assert.Equal(expectedEvent.EventId, actualEvent.EventId);
            Assert.Equal(expectedEvent.LotId, actualEvent.LotId);
            Assert.Equal(expectedEvent.Name, actualEvent.Name);
            Assert.Equal(expectedEvent.Category, actualEvent.Category);
            Assert.Equal(expectedEvent.Quantity, actualEvent.Quantity);
            Assert.Equal(
                expectedEvent.EstimatedValue,
                actualEvent.EstimatedValue);

            handlerMock.Verify(
                handler => handler.HandleAsync(
                    It.IsAny<LotCreatedIntegrationEvent>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            await consumer.StopAsync(CancellationToken.None);

        }
    }
}
