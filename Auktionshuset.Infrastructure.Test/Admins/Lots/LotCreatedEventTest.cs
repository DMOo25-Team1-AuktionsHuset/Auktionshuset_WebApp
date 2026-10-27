using Auktionshuset.Application.Admin.Lots.CreateLot;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Infrastructure.Messaging.Consumers;
using Auktionshuset.Infrastructure.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using Auktionshuset.Infrastructure.Messaging.InboxServices;
using Auktionshuset.Infrastructure.Messaging.RabbitMqServices;

namespace Auktionshuset.Infrastructure.Test.Admins.Lots
{
    //public class LotCreatedEventTest
    //{
    //    /// <summary>
    //    /// Round-trips a lot-created event through RabbitMQ to verify the exchange, binding and
    //    /// routing key. Requires a local RabbitMQ broker.
    //    /// </summary>
    //    [Fact]
    //    [Trait("Category", "Integration")]
    //    public async Task LotCreatedEvent_CanBePublishedToRabbitMq()
    //    {
    //        // Arrange 
    //        ConnectionFactory factory = new ConnectionFactory
    //        {
    //            HostName = "localhost",
    //            UserName = "guest",
    //            Password = "guest"
    //        };

    //        await using IConnection connection =
    //            await factory.CreateConnectionAsync();

    //        await using IChannel channel =
    //            await connection.CreateChannelAsync();

    //        const string exchange = "auktionshuset.events";
    //        const string queue = "test.lot-created";
    //        const string routingKey = "lot.created.v1";

    //        await channel.ExchangeDeclareAsync(
    //            exchange,
    //            ExchangeType.Topic,
    //            durable: true,
    //            autoDelete: false);

    //        await channel.QueueDeclareAsync(
    //            queue,
    //            durable: false,
    //            exclusive: true,
    //            autoDelete: true);

    //        await channel.QueueBindAsync(
    //            queue,
    //            exchange,
    //            routingKey);

    //        // Act
    //        LotCreatedIntegrationEvent eventMessage = new LotCreatedIntegrationEvent(
    //            EventId: Guid.NewGuid(),
    //            LotId: Guid.NewGuid(),
    //            AuctionHouseId: Guid.NewGuid(),
    //            Name: "Test Lot",
    //            Category: "Test Category",
    //            Quantity: 1,
    //            EstimatedValue: 100,
    //            OccurredAt: DateTime.UtcNow);

    //        string json = JsonSerializer.Serialize(eventMessage);
    //        byte[] body = Encoding.UTF8.GetBytes(json);

    //        await channel.BasicPublishAsync(
    //            exchange,
    //            routingKey,
    //            body);

    //        BasicGetResult? result = await channel.BasicGetAsync(
    //            queue,
    //            autoAck: true);

    //        // Assert
    //        Assert.NotNull(result);

    //        string receivedJson =
    //            Encoding.UTF8.GetString(result!.Body.ToArray());

    //        LotCreatedIntegrationEvent? receivedEvent =
    //            JsonSerializer.Deserialize<LotCreatedIntegrationEvent>(
    //                receivedJson);

    //        Assert.NotNull(receivedEvent);
    //        Assert.Equal(eventMessage.LotId, receivedEvent!.LotId);
    //        Assert.Equal(eventMessage.Name, receivedEvent.Name);
    //        Assert.Equal("lot.created.v1", result.RoutingKey);
    //    }

    //    /// <summary>
    //    /// A delivery is acknowledged only after the inbox has accepted it.
    //    /// </summary>
    //    [Theory]
    //    [InlineData(InboxTypes.Stored, false)]
    //    [InlineData(InboxTypes.Duplicate, false)]
    //    [InlineData(InboxTypes.Quarantined, false)]
    //    [InlineData(InboxTypes.Stored, true)]
    //    public async Task AdminConsumer_PersistsDeliveryBeforeAcknowledging(InboxTypes result, bool storageFails)
    //    {
    //        byte[] payload = "{\"test\":true}"u8.ToArray();
    //        string messageId = Guid.NewGuid().ToString("D");
    //        var persisted = new TaskCompletionSource<InboxTypes>(TaskCreationOptions.RunContinuationsAsynchronously);
    //        var subscribed = new TaskCompletionSource<IAsyncBasicConsumer>(TaskCreationOptions.RunContinuationsAsynchronously);
    //        var inbox = new Mock<IInboxStore>();
    //        inbox.Setup(store => store.StoreNewMessageAsync(
    //                RabbitMqTopology.Queues.Admin, "lot.created.v1", messageId,
    //                It.Is<byte[]>(bytes => bytes.SequenceEqual(payload)), It.IsAny<CancellationToken>()))
    //            .Returns(persisted.Task);

    //        var services = new ServiceCollection();
    //        services.AddScoped<IInboxStore>(_ => inbox.Object);
    //        await using ServiceProvider serviceProvider = services.BuildServiceProvider();
    //        var channel = new Mock<IChannel>();
    //        channel.Setup(value => value.BasicConsumeAsync(
    //                It.IsAny<string>(), false, It.IsAny<string>(), false, false,
    //                It.IsAny<IDictionary<string, object?>>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()))
    //            .Callback(new InvocationAction(invocation =>
    //                subscribed.TrySetResult((IAsyncBasicConsumer)invocation.Arguments[6])))
    //            .ReturnsAsync("test-consumer");
    //        var connection = new Mock<IConnection>();
    //        connection.Setup(value => value.CreateChannelAsync(
    //                It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
    //            .ReturnsAsync(channel.Object);

    //        using var consumer = new AdminEventsConsumer(
    //            new TestConnectionProvider(connection.Object),
    //            new RabbitMqTopologyInitializer(new EventContractRegistry()),
    //            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
    //            NullLogger<AdminEventsConsumer>.Instance);
    //        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    //        await consumer.StartAsync(timeout.Token);
    //        IAsyncBasicConsumer deliveryConsumer = await subscribed.Task.WaitAsync(timeout.Token);
    //        Task delivery = deliveryConsumer.HandleBasicDeliverAsync(
    //            "test-consumer", 1, false, RabbitMqTopology.EventExchange, "lot.created.v1",
    //            new BasicProperties { MessageId = messageId }, payload, timeout.Token);

    //        channel.Verify(value => value.BasicAckAsync(1, false, It.IsAny<CancellationToken>()), Times.Never);
    //        if (storageFails)
    //            persisted.SetException(new IOException("Inbox storage unavailable"));
    //        else
    //            persisted.SetResult(result);
    //        await delivery.WaitAsync(timeout.Token);

    //        inbox.VerifyAll();
    //        channel.Verify(value => value.BasicAckAsync(1, false, It.IsAny<CancellationToken>()),
    //            storageFails ? Times.Never() : Times.Once());
    //        await consumer.StopAsync(timeout.Token);
    //    }

    //    private sealed class TestConnectionProvider(IConnection connection) : IRabbitMqConnectionProvider
    //    {
    //        public Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken) => Task.FromResult(connection);
    //    }
    //}
}
