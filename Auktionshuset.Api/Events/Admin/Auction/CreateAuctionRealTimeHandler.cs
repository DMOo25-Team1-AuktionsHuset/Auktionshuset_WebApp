using Auktionshuset.Api.Hubs;
using Auktionshuset.Application.Admin.Auctions.CreateAuction;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Contracts.Dto.Admin.Auction;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Events.Admin.Auction {
    public class CreateAuctionRealTimeHandler(IHubContext<AuctionHub, IAuctionClient> hubContext) : IIntegrationEventHandler<AuctionCreatedIntegrationEvent> {
        public Task HandleAsync(AuctionCreatedIntegrationEvent message, CancellationToken cancellationToken) {
            var notification = new CreateAuctionNotification(
                EventId: message.EventId,
                AuctionId: message.AuctionId,
                StartsAt: message.StartsAt,
                LotCount: message.LotCount,
                OccurredAt: message.OccurredAt);

            return hubContext.Clients.All.AuctionCreatedAsync(notification);
        }
    }
}
