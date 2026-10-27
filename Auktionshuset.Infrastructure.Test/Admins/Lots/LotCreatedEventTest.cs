using Auktionshuset.Application.Admin.Lots.CreateLot;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Infrastructure.Messaging;
using Auktionshuset.Infrastructure.Messaging.Consumers;
using Auktionshuset.Infrastructure.Messaging.InboxServices;
using Auktionshuset.Infrastructure.Messaging.MessagingServices;
using Auktionshuset.Infrastructure.Messaging.RabbitMqServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Auktionshuset.Infrastructure.Test.Admins.Lots;

public sealed class LotCreatedEventTest
{
    [Fact]
    public async Task LotCreatedEvent_PublishesRegisteredRouteAndJsonPayload()
    {
        var registry = new EventContractRegistry();
        var channel = new Mock<IChannel>();
        var connection = new Mock<IConnection>();
        connection.Setup(value => value.CreateChannelAsync(
                It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel.Object);

        string? publishedExchange = null;
        string? publishedRoutingKey = null;
        BasicProperties? publishedProperties = null;
        byte[]? publishedBody = null;

        channel.Setup(value => value.BasicPublishAsync<BasicProperties>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, BasicProperties, ReadOnlyMemory<byte>, CancellationToken>(
                (exchange, routingKey, _, properties, body, _) =>
                {
                    publishedExchange = exchange;
                    publishedRoutingKey = routingKey;
                    publishedProperties = properties;
                    publishedBody = body.ToArray();
                })
            .Returns(ValueTask.CompletedTask);

        var publisher = new RabbitMqIntegrationEventPublisher(
            new TestConnectionProvider(connection.Object),
            new RabbitMqTopologyInitializer(registry),
            registry,
            NullLogger<RabbitMqIntegrationEventPublisher>.Instance);
        await using (publisher)
        {
            var message = new LotCreatedIntegrationEvent(
                EventId: Guid.NewGuid(),
                LotId: Guid.NewGuid(),
                AuctionHouseId: Guid.NewGuid(),
                Name: "Test Lot",
                Category: "Test Category",
                Quantity: 2,
                EstimatedValue: 100m,
                OccurredAt: new DateTime(2030, 1, 15, 12, 0, 0, DateTimeKind.Utc));

            await publisher.PublishAsync(message, CancellationToken.None);

            Assert.Equal(RabbitMqTopology.EventExchange, publishedExchange);
            Assert.Equal("lot.created.v1", publishedRoutingKey);
            Assert.NotNull(publishedProperties);
            Assert.Equal(message.EventId.ToString("D"), publishedProperties.MessageId);
            Assert.Equal("lot.created.v1", publishedProperties.Type);
            Assert.NotNull(publishedBody);

            LotCreatedIntegrationEvent received = Assert.IsType<LotCreatedIntegrationEvent>(
                JsonSerializer.Deserialize<LotCreatedIntegrationEvent>(Encoding.UTF8.GetString(publishedBody)));
            Assert.Equal(message.EventId, received.EventId);
            Assert.Equal(message.LotId, received.LotId);
            Assert.Equal(message.Name, received.Name);
            Assert.Equal(message.Category, received.Category);
            Assert.Equal(message.Quantity, received.Quantity);
            Assert.Equal(message.EstimatedValue, received.EstimatedValue);

            channel.Verify(value => value.QueueBindAsync(
                    RabbitMqTopology.Queues.Admin,
                    RabbitMqTopology.EventExchange,
                    "lot.created.v1",
                    It.IsAny<IDictionary<string, object?>>(),
                    false,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    [Theory]
    [InlineData(InboxTypes.Stored, false, true)]
    [InlineData(InboxTypes.Duplicate, false, true)]
    [InlineData(InboxTypes.Quarantined, false, true)]
    [InlineData(InboxTypes.Stored, true, false)]
    public async Task AdminConsumer_AcknowledgesOnlyAfterInboxPersistsDelivery(
        InboxTypes inboxResult,
        bool storageFails,
        bool shouldAcknowledge)
    {
        byte[] payload = "{\"test\":true}"u8.ToArray();
        string messageId = Guid.NewGuid().ToString("D");
        var persisted = new TaskCompletionSource<InboxTypes>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscribed = new TaskCompletionSource<IAsyncBasicConsumer>(TaskCreationOptions.RunContinuationsAsynchronously);
        var inbox = new Mock<IInboxStore>();
        inbox.Setup(store => store.StoreNewMessageAsync(
                RabbitMqTopology.Queues.Admin,
                "lot.created.v1",
                messageId,
                It.Is<byte[]>(bytes => bytes.SequenceEqual(payload)),
                It.IsAny<CancellationToken>()))
            .Returns(persisted.Task);

        var services = new ServiceCollection();
        services.AddScoped<IInboxStore>(_ => inbox.Object);
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        var channel = new Mock<IChannel>();
        channel.Setup(value => value.BasicConsumeAsync(
                It.IsAny<string>(),
                false,
                It.IsAny<string>(),
                false,
                false,
                It.IsAny<IDictionary<string, object?>>(),
                It.IsAny<IAsyncBasicConsumer>(),
                It.IsAny<CancellationToken>()))
            .Callback(new InvocationAction(invocation =>
                subscribed.TrySetResult((IAsyncBasicConsumer)invocation.Arguments[6])))
            .ReturnsAsync("test-consumer");

        var connection = new Mock<IConnection>();
        connection.Setup(value => value.CreateChannelAsync(
                It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel.Object);

        using var runtime = new MessagingRuntime(
            TimeProvider.System,
            Options.Create(new MessagingOperationsOptions { RunWorkers = false }));
        using var consumer = new AdminEventsConsumer(
            new TestConnectionProvider(connection.Object),
            new RabbitMqTopologyInitializer(new EventContractRegistry()),
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AdminEventsConsumer>.Instance,
            runtime);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await consumer.StartAsync(timeout.Token);
        IAsyncBasicConsumer deliveryConsumer = await subscribed.Task.WaitAsync(timeout.Token);
        Task delivery = deliveryConsumer.HandleBasicDeliverAsync(
            "test-consumer",
            1,
            false,
            RabbitMqTopology.EventExchange,
            "lot.created.v1",
            new BasicProperties { MessageId = messageId },
            payload,
            timeout.Token);

        channel.Verify(value => value.BasicAckAsync(1, false, It.IsAny<CancellationToken>()), Times.Never);
        if (storageFails)
        {
            persisted.SetException(new IOException("Inbox storage unavailable"));
        }
        else
        {
            persisted.SetResult(inboxResult);
        }

        await delivery.WaitAsync(timeout.Token);
        inbox.VerifyAll();
        channel.Verify(value => value.BasicAckAsync(1, false, It.IsAny<CancellationToken>()),
            shouldAcknowledge ? Times.Once() : Times.Never());
        await consumer.StopAsync(timeout.Token);
    }

    private sealed class TestConnectionProvider(IConnection connection) : IRabbitMqConnectionProvider
    {
        public Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken) =>
            Task.FromResult(connection);
    }
}