using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.EventHandling;
using Microsoft.Extensions.DependencyInjection;
using Auktionshuset.Infrastructure.Messaging.Consumers;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Auktionshuset.Application.Admin.Lots.CreateLot;
using Auktionshuset.Infrastructure.Messaging.Consumers.Lot;

namespace Auktionshuset.Infrastructure.Test.Admins.Lots
{
    public class LotCreatedEventTest
    {
        [Fact]
        public async Task LotCreatedEvent_CanBePublishedToRabbitMq()
        {
            // Arrange 
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest"
            };

            await using var connection =
                await factory.CreateConnectionAsync();

            await using var channel =
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

            var json = JsonSerializer.Serialize(eventMessage);
            var body = Encoding.UTF8.GetBytes(json);

            await channel.BasicPublishAsync(
                exchange,
                routingKey,
                body);

            var result = await channel.BasicGetAsync(
                queue,
                autoAck: true);

            // Assert
            Assert.NotNull(result);

            var receivedJson =
                Encoding.UTF8.GetString(result!.Body.ToArray());

            var receivedEvent =
                JsonSerializer.Deserialize<LotCreatedIntegrationEvent>(
                    receivedJson);

            Assert.NotNull(receivedEvent);
            Assert.Equal(eventMessage.LotId, receivedEvent!.LotId);
            Assert.Equal(eventMessage.Name, receivedEvent.Name);
            Assert.Equal("lot.created.v1", result.RoutingKey);
        }

        [Fact]
        public async Task Consumer_CallsHandler_WhenLotCreatedEventIsReceived()
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

            await using var serviceProvider =
                services.BuildServiceProvider();

            var scopeFactory =
                serviceProvider.GetRequiredService<IServiceScopeFactory>();

            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest"
            };

            await using var connection =
                await factory.CreateConnectionAsync();

            await using var publishChannel =
                await connection.CreateChannelAsync();

            const string exchangeName = "auktionshuset.events";
            const string queueName = "auktionshuset.lot-created";
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
            new LotCreatedConsumer(connection, scopeFactory);

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

            var json = JsonSerializer.Serialize(expectedEvent);
            var body = Encoding.UTF8.GetBytes(json);

            await publishChannel.BasicPublishAsync(
                exchange: exchangeName,
                routingKey: routingKey,
                body: body);

            var actualEvent =
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
