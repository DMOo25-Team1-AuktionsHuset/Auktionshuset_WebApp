using Auktionshuset.Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Hubs;

// This hub sends only invalidations. All data is fetched through customer-scoped HTTP endpoints.
// There is deliberately no client-selected customer or auction subscription.
public sealed class CustomerAuctionHub(CustomerProfileService profiles) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var own = await profiles.GetOwnProfileAsync(Context.User!, Context.ConnectionAborted);
        if (own.Profile is null) { Context.Abort(); return; }
        await base.OnConnectedAsync();
    }
}
