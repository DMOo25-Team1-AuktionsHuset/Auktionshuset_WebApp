using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Auktionshuset.Infrastructure.Messaging.Consumers;

internal sealed class AdminEventsConsumer(
    IRabbitMqConnectionProvider connections,
    RabbitMqTopologyInitializer topology,
    IServiceScopeFactory scopeFactory,
    ILogger<AdminEventsConsumer> logger)
    : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSessionAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "RabbitMq consumer stopped. New retry in {RetrySeconds} seconds", RetryDelay.TotalSeconds);
            }

            try
            {
                await Task.Delay(RetryDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunSessionAsync(CancellationToken stoppingToken)
    {
        using var session = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        var ended = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        IConnection connection = await connections.GetConnectionAsync(session.Token);

        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: session.Token);

        try
        {
            channel.ChannelShutdownAsync += (_, reason) =>
            {
                ended.TrySetResult(new IOException(
                    $"RabbitMq channel closed: {reason.ReplyCode} {reason.ReplyText}"));

                return Task.CompletedTask;
            };

            channel.CallbackExceptionAsync += (_, args) =>
            {
                ended.TrySetResult(args.Exception);

                return Task.CompletedTask;
            };

            await topology.EnsureAsync(
                channel,
                session.Token);

            await channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: 1,
                global: false,
                cancellationToken: session.Token);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.UnregisteredAsync += (_, _) =>
            {
                ended.TrySetResult(new IOException(
                    "RabbitMq consumer was unregistered"));

                return Task.CompletedTask;
            };

            consumer.ReceivedAsync += async (_, eventArgs) =>
            {
                try
                {
                    using var delivery = CancellationTokenSource.CreateLinkedTokenSource(
                        session.Token, eventArgs.CancellationToken);

                    byte[] payload = eventArgs.Body.ToArray();

                    await using AsyncServiceScope scope =
                     scopeFactory.CreateAsyncScope();

                    var inbox = scope.ServiceProvider.GetRequiredService<IInboxStore>();


                    InboxTypes inboxResult = await inbox.StoreNewMessageAsync(
                        consumerName: RabbitMqTopology.Queues.Admin,
                        eventType: eventArgs.RoutingKey,
                        transportMessageId: eventArgs.BasicProperties.MessageId,
                        payload: payload,
                        cancellationToken: delivery.Token);

                    await channel.BasicAckAsync(
                        deliveryTag: eventArgs.DeliveryTag,
                        multiple: false,
                        cancellationToken: delivery.Token);

                    if (inboxResult == InboxTypes.Quarantined)
                    {
                        logger.LogWarning("Message quarantined: {MessageId}", eventArgs.BasicProperties.MessageId);
                    }
                }
                catch (Exception ex)
                {
                    // No ACK, the outer session ensures channel cleanup and restart
                    ended.TrySetResult(ex);
                }
            };

            await channel.BasicConsumeAsync(
                queue: RabbitMqTopology.Queues.Admin,
                autoAck: false,
                consumer: consumer,
                cancellationToken: session.Token);

            logger.LogInformation(
                "RabbitMq consumer listening to {QueueName}", RabbitMqTopology.Queues.Admin);

            Exception failure = await ended.Task.WaitAsync(stoppingToken);

            throw new IOException("RabbitMQ consumers session was interrupted", failure);
        }
        finally
        {
            // Stop eventual in processing before channel is disposed
            await session.CancelAsync();
        }
    }
}
