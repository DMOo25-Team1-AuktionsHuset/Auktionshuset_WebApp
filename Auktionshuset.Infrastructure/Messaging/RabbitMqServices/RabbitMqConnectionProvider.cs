using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Auktionshuset.Infrastructure.Messaging.RabbitMqServices
{
    internal class RabbitMqConnectionProvider(
        ConnectionFactory factory,
        ILogger<RabbitMqConnectionProvider> logger)
        : IRabbitMqConnectionProvider, IAsyncDisposable
    {
        private readonly SemaphoreSlim gate = new(1, 1);

        private IConnection? connection;
        private bool disposed;

        public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken);

            try
            {
                ObjectDisposedException.ThrowIf(disposed, this);

                if (connection is { IsOpen: true })
                {
                    return connection;
                }

                await DisposeConnectionAsync();

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                timeout.CancelAfter(TimeSpan.FromSeconds(10));

                connection = await factory.CreateConnectionAsync(cancellationToken: timeout.Token);

                logger.LogInformation("RabbitMq connection established");

                return connection;
            }
            finally
            {
                gate.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await gate.WaitAsync();

            try
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;

                await DisposeConnectionAsync();
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task DisposeConnectionAsync()
        {
            IConnection? previous = connection;
            connection = null;

            if (previous is null)
            {
                return;
            }

            try
            {
                await previous.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error during cleanup of RabbitMq connection");
            }
        }
    }
}
