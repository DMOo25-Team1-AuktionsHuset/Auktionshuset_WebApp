using Auktionshuset.Api.Hubs;
using Auktionshuset.Application.Admin.Auctions.Bids;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Contracts.Dto.Admin.Auction;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Events.Admin.Auction
{
    public class BidPlacedRealTimeHandler(IHubContext<AuctionHub, IAuctionClient> hubContext, IHubContext<CustomerAuctionHub> customers) : IIntegrationEventHandler<BidPlacedIntegrationEvent>
    {
        public async Task HandleAsync(BidPlacedIntegrationEvent message, CancellationToken cancellationToken)
        {
            var notification = new BidPlacedNotification(
                EventId: message.EventId,
                BidId: message.BidId,
                AuctionLotId: message.AuctionLotId,
                Amount: message.Amount,
                SequenceNumber: message.SequenceNumber,
                OccurredAt: message.OccurredAt);

            await customers.Clients.All.SendAsync("AuctionsChanged", Guid.Empty, cancellationToken);
            await hubContext.Clients.All.BidPlacedAsync(notification);
        }
    }
}
