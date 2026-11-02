using Auktionshuset.Api.Hubs;
using Auktionshuset.Application.Admin.Auctions.CloseAuction;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Contracts.Dto.Admin.Auction;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Events.Admin.Auction
{
    public sealed class AuctionLotClosedRealTimeHandler(
        IHubContext<AuctionHub, IAuctionClient> hubContext, IHubContext<CustomerAuctionHub> customers)
        : IIntegrationEventHandler<AuctionLotClosedIntegrationEvent>
    {
        public async Task HandleAsync(
            AuctionLotClosedIntegrationEvent message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await customers.Clients.All.SendAsync("AuctionsChanged", Guid.Empty, cancellationToken);
            await hubContext.Clients.All.AuctionLotClosedAsync(
                new AuctionLotClosedNotification(
                    EventId: message.EventId,
                    AuctionLotId: message.AuctionLotId));
        }
    }
}
