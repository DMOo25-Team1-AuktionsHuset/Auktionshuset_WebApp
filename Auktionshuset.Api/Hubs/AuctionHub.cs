using Auktionshuset.Contracts.Dto.Admin.Auction;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Hubs
{
    public class AuctionHub : Hub<IAuctionClient>
    {
    }
}
