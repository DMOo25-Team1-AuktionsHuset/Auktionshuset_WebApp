using Auktionshuset.Contracts.Dto.Admin.Auction;
using Microsoft.AspNetCore.SignalR.Client;

namespace Auktionshuset.Services;

/// <summary>
/// Keeps a SignalR connection to the auction hub so connected clients are
/// notified as soon as an auction is created — including auctions created on
/// another server instance, since the event is fanned out via the message bus.
/// </summary>
public sealed class AuctionRealtimeService : IAsyncDisposable
{
    private readonly string hubUrl;
    private readonly SemaphoreSlim startGate = new(1, 1);
    private HubConnection? connection;

    public AuctionRealtimeService(IConfiguration configuration)
    {
        var apiBaseUrl = configuration["Api:BaseUrl"]
            ?? throw new InvalidOperationException("Configuration value 'Api:BaseUrl' is required.");

        hubUrl = $"{apiBaseUrl.TrimEnd('/')}/hubs/auction";
    }

    public event Func<CreateAuctionNotification, Task>? AuctionCreated;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await startGate.WaitAsync(cancellationToken);

        try
        {
            if (connection is not null)
            {
                return;
            }

            var hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            hubConnection.On<CreateAuctionNotification>(
                nameof(IAuctionClient.AuctionCreatedAsync),
                notification => AuctionCreated?.Invoke(notification) ?? Task.CompletedTask);

            try
            {
                await hubConnection.StartAsync(cancellationToken);
            }
            catch
            {
                await hubConnection.DisposeAsync();
                throw;
            }

            connection = hubConnection;
        }
        finally
        {
            startGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null)
        {
            await connection.DisposeAsync();
            connection = null;
        }

        startGate.Dispose();
    }
}
