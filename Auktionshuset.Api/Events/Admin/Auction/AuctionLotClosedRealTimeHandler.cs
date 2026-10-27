using Auktionshuset.Api.Hubs;
using Auktionshuset.Application.Admin.Auctions.CloseAuction;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Contracts.Dto.Admin.Auction;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Events.Admin.Auction
{
    public sealed class AuctionLotClosedRealTimeHandler(
        IHubContext<AuctionHub, IAuctionClient> hubContext)
        : IIntegrationEventHandler<AuctionLotClosedIntegrationEvent>
    {
        public Task HandleAsync(
            AuctionLotClosedIntegrationEvent message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return hubContext.Clients.All.AuctionLotClosedAsync(
                new AuctionLotClosedNotification(
                    EventId: message.EventId,
                    AuctionLotId: message.AuctionLotId));
        }
    }
}
