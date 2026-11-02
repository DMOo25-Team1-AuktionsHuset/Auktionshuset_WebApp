using Auktionshuset.Security;
using Microsoft.AspNetCore.SignalR.Client;

namespace Auktionshuset.Services;

public sealed class CustomerAuctionRealtimeService(
    IConfiguration configuration, BackendTokenAccessor tokens, ILogger<CustomerAuctionRealtimeService> logger)
    : RealtimeServiceBase("hubs/customer-auctions", configuration, tokens, logger)
{
    protected override void ConfigureSubscriptions(HubConnection connection) =>
        InvalidateOn<Guid>(connection, "AuctionsChanged");
}
