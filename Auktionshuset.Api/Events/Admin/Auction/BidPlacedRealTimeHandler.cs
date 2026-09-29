using Auktionshuset.Api.Hubs;
using Auktionshuset.Application.Admin.Auctions.Bids;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Contracts.Dto.Admin.Auction;
using Auktionshuset.Contracts.Dto.Auction;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Events.Admin.Auction
{
    public class BidPlacedRealTimeHandler(IHubContext<AuctionHub, IAuctionClient> hubContext) : IIntegrationEventHandler<BidPlacedIntegrationEvent>
    {
        public Task HandleAsync(BidPlacedIntegrationEvent message, CancellationToken cancellationToken)
        {
            var notification = new BidPlacedNotification(
                EventId: message.EventId,
                BidId: message.BidId,
                AuctionLotId: message.AuctionLotId,
                Amount: message.Amount,
                SequenceNumber: message.SequenceNumber,
                OccurredAt: message.OccurredAt);

            return hubContext.Clients.All.BidPlacedAsync(notification);
        }
    }
}
