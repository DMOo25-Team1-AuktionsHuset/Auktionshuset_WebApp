using Auktionshuset.Security;
using Microsoft.AspNetCore.SignalR.Client;

namespace Auktionshuset.Services
{
    public abstract class RealtimeServiceBase : IAsyncDisposable
    {
        private readonly string _hubUrl;
        private readonly BackendTokenAccessor _tokenAccessor;
        private readonly ILogger _logger;

        private readonly SemaphoreSlim _startGate = new(1, 1);
        private readonly CancellationTokenSource _lifetime = new();

        private HubConnection? _connection;
        private Task? _runner;

        private bool _isSessionValid = true;
        private bool _disposed;

        protected RealtimeServiceBase(
            string hubPath,
            IConfiguration configuration,
            BackendTokenAccessor tokenAccessor,
            ILogger logger)
        {
            this._tokenAccessor = tokenAccessor;
            this._logger = logger;

            string apiBaseUrl = configuration["Api:BaseUrl"]
                ?? throw new InvalidOperationException(
                    "Configuration value: 'Api:BaseUrl' is required.");

            _hubUrl = $"{apiBaseUrl.TrimEnd('/')}/{hubPath.TrimStart('/')}";
        }

        public bool isConnected =>
            _connection?.State == HubConnectionState.Connected;

        public event Func<Task>? Invalidated;
        public event Func<Task>? ConnectionChanged;

        protected abstract void ConfigureSubscriptions(
            HubConnection hubConnection);

        protected void InvalidateOn<TNotification>(
            HubConnection hubConnection,
            string methodName)
        {
            hubConnection.On<TNotification>(
                methodName,
                _ => NotifyAsync(Invalidated));
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            await _startGate.WaitAsync(cancellationToken);

            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                if (_connection != null)
                    return;

                _connection = new HubConnectionBuilder()
                    .WithUrl(_hubUrl, options =>
                        options.AccessTokenProvider = GetAccessTokenAsync)
                    .WithAutomaticReconnect(
                        new SessionReconnectPolicy(() => _isSessionValid))
                    .Build();

                ConfigureSubscriptions(_connection);

                _connection.Reconnecting +=
                    _ => NotifyAsync(ConnectionChanged);

                _connection.Reconnected += async _ =>
                {
                    await NotifyAsync(ConnectionChanged);

                    // Events can be list under interrupt/disconnect
                    await NotifyAsync(Invalidated);
                };

                _connection.Closed +=
                    _ => NotifyAsync(ConnectionChanged);

                _runner = RunAsync(_connection, _lifetime.Token);
            }
            finally
            {
                _startGate.Release();
            }
        }

        private async Task RunAsync(
            HubConnection hubConnection,
            CancellationToken cancellationToken)
        {
            await Task.Yield();

            try
            {
                while (!cancellationToken.IsCancellationRequested
                    && _isSessionValid)
                {
                    if (hubConnection.State == HubConnectionState.Disconnected)
                    {
                        try
                        {
                            await hubConnection.StartAsync(cancellationToken);

                            await NotifyAsync(ConnectionChanged);

                            // close hole between first Get and connection
                            await NotifyAsync(Invalidated);
                        }
                        catch (OperationCanceledException)
                            when (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(
                                ex,
                                $"SignalR could not connect to {_hubUrl}");

                            await NotifyAsync(ConnectionChanged);
                        }
                    }

                    await Task.Delay(
                        TimeSpan.FromSeconds(5),
                        cancellationToken);
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        private async Task<string?> GetAccessTokenAsync()
        {
            string? accessToken = await _tokenAccessor.GetAccessTokenAsync();

            _isSessionValid = !string.IsNullOrWhiteSpace(accessToken);

            if (!_isSessionValid)
                throw new InvalidOperationException("Session no longer has a valid accesstoken");

            return accessToken;
        }

        private async Task NotifyAsync(Func<Task>? subscribers)
        {
            if (_disposed || subscribers is null)
            {
                return;
            }

            foreach (Func<Task> subscriber in subscribers
                .GetInvocationList()
                .Cast<Func<Task>>())
            {
                try
                {
                    await subscriber();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex, 
                        "a frontend update failed af signalr failed");
                }
            }
        } 

        public async ValueTask DisposeAsync()
        {
            await _startGate.WaitAsync();

            try
            {
                if (_disposed)
                    return;

                _disposed = true;

                await _lifetime.CancelAsync();

                if (_runner != null)
                {
                    await _runner;
                }

                if (_connection != null)
                {
                    await _connection.DisposeAsync();
                    _connection = null;
                }

                _lifetime.Dispose();
            }
            finally
            {
                _startGate.Release();
            }
        }
    }
}
