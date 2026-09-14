using Auktionshuset.Application.Admin.Lots;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

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
                durable: false,
                autoDelete: true);

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
    }
}
