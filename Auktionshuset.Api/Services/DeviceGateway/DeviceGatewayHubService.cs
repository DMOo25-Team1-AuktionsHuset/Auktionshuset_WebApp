using Auktionshuset.Api.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;

namespace Auktionshuset.Api.Services.DeviceGateway;

public sealed class DeviceGatewayHubService(
    IConfiguration configuration,
    IHostEnvironment environment,
    DeviceGatewayProcessor gateway,
    ILogger<DeviceGatewayHubService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var url = configuration["DeviceGateway:HubUrl"];
        if (string.IsNullOrWhiteSpace(url))
        {
            logger.LogInformation("Device gateway hub client is disabled: DeviceGateway:HubUrl is not configured");
            return;
        }

        if (!string.IsNullOrWhiteSpace(configuration["DeviceGateway:AccessToken"]))
            throw new InvalidOperationException("DeviceGateway:AccessToken is no longer supported; configure DeviceGateway:Authentication instead.");
        var uri = DeviceGatewayClientSecurity.ValidateHubUrl(url, environment.IsDevelopment(),
            configuration.GetValue<bool>("DeviceGateway:AllowInsecureLoopback"));
        // Fail configuration errors before entering the retry loop.
        _ = DeviceGatewayClientSecurity.CreateToken(configuration);

        while (!stoppingToken.IsCancellationRequested)
        {
            var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var stateLock = new object();
            void Disconnect()
            {
                lock (stateLock)
                {
                    gateway.SetConnected(false);
                    disconnected.TrySetResult();
                }
            }

            await using var connection = new HubConnectionBuilder().WithUrl(uri, options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.AccessTokenProvider = () => Task.FromResult<string?>(DeviceGatewayClientSecurity.CreateToken(configuration));
                options.HttpMessageHandlerFactory = handler =>
                {
                    if (handler is HttpClientHandler clientHandler)
                        clientHandler.AllowAutoRedirect = false;
                    else if (handler is SocketsHttpHandler socketsHandler)
                        socketsHandler.AllowAutoRedirect = false;
                    else
                        throw new InvalidOperationException("Cannot disable redirects for the device gateway connection.");
                    return handler;
                };
            }).Build();

            async Task ForwardAsync(Func<Task> forward)
            {
                try
                {
                    if (!disconnected.Task.IsCompleted)
                        await forward();
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(ex, "Device gateway notification failed; reconnecting and resynchronizing");
                    Disconnect();
                }
            }

            connection.On<Guid, Guid, long>(nameof(IDeviceGatewayClient.AuctionChanged),
                (eventId, auctionId, revision) => ForwardAsync(() => gateway.AuctionChangedAsync(eventId, auctionId, revision, stoppingToken)));
            connection.On<Guid, Guid, Guid>(nameof(IDeviceGatewayClient.AssignmentChanged),
                (eventId, deviceId, sessionId) => ForwardAsync(() => gateway.AssignmentChangedAsync(eventId, deviceId, sessionId, stoppingToken)));
            connection.Closed += _ => { Disconnect(); return Task.CompletedTask; };

            try
            {
                await connection.StartAsync(stoppingToken);
                await gateway.SynchronizeConnectionAsync(stoppingToken);
                lock (stateLock)
                {
                    if (!disconnected.Task.IsCompleted)
                        gateway.SetConnected(true);
                }

                await disconnected.Task.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                logger.LogWarning("Device gateway hub connection or resynchronization failed ({ErrorType})", ex.GetType().Name);
            }
            finally
            {
                gateway.SetConnected(false);
                try { await gateway.SetUnavailableAsync(CancellationToken.None); }
                catch (Exception ex) { logger.LogError(ex, "Could not mark all gateway devices unavailable"); }
                try { await connection.StopAsync(CancellationToken.None); }
                catch (Exception ex) { logger.LogWarning("Could not stop device gateway hub connection cleanly ({ErrorType})", ex.GetType().Name); }
            }

            if (!stoppingToken.IsCancellationRequested)
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
