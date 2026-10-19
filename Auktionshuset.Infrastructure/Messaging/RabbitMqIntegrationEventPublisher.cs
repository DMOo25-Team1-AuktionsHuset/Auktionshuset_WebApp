using Auktionshuset.Application.EventHandling;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace Auktionshuset.Infrastructure.Messaging
{
    internal sealed class RabbitMqIntegrationEventPublisher(
        IRabbitMqConnectionProvider connections,
        RabbitMqTopologyInitializer topology,
        EventContractRegistry registry,
        ILogger<RabbitMqIntegrationEventPublisher> logger) 
        : IIntegrationEventPublisher, IAsyncDisposable
    {
        private readonly SemaphoreSlim gate = new(1, 1);

        private IChannel? channel;
        private bool disposed;

        public async Task PublishAsync<TEvent>(
            TEvent message,
            CancellationToken cancellationToken)
            where TEvent : IIntegrationEvent

        {
            cancellationToken.ThrowIfCancellationRequested();

            EventContractRegistry.EventContractRecord contract = registry.ByType(message.GetType());

            byte[] body = Encoding.UTF8.GetBytes(
                registry.Serialize(message));

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                MessageId = message.EventId.ToString("D"),
                Type = contract.EventContractName
            };

            await gate.WaitAsync(cancellationToken);

            try
            {
                ObjectDisposedException.ThrowIf(disposed, this);

                IChannel publishingChannel = await GetChannelAsync(cancellationToken);

                await publishingChannel.BasicPublishAsync(
                    exchange: RabbitMqTopology.EventExchange,
                    routingKey: contract.EventContractName,
                    mandatory: true,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: cancellationToken);
            }
            catch
            {
                await DisposeChannelAsync();

                throw;
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
        {
            if (channel is { IsOpen: true })
                return channel;

            await DisposeChannelAsync();

            IConnection connection = await connections.GetConnectionAsync(cancellationToken);

            var options = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true);

            channel = await connection.CreateChannelAsync(options, cancellationToken);

            await topology.EnsureAsync(channel, cancellationToken);

            return channel;
        }

        public async ValueTask DisposeAsync()
        {
            await gate.WaitAsync();

            try
            {
                if (disposed)
                    return;

                disposed = true;

                await DisposeChannelAsync();
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task DisposeChannelAsync()
        {
            IChannel? previous = channel;
            channel = null;

            if (previous is null)
                return;

            try
            {
                await previous.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error during cleanup of publishing channel");
            }
        }
    }
}
