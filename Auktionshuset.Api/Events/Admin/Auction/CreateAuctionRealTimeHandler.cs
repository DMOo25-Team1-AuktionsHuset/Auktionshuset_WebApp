using Auktionshuset.Api.Hubs;
using Auktionshuset.Application.Admin.Auctions.CreateAuction;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Contracts.Dto.Admin.Auction;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Events.Admin.Auction {
    public class CreateAuctionRealTimeHandler(IHubContext<AuctionHub, IAuctionClient> hubContext) : IIntegrationEventHandler<AuctionCreatedIntegrationEvent> {
        /// <summary>
        /// Broadcasts an auction-created notification to all connected clients.
        /// </summary>
        /// <param name="message">The integration event describing the created auction.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>A task that completes once the notification has been broadcast.</returns>
        public Task HandleAsync(AuctionCreatedIntegrationEvent message, CancellationToken cancellationToken) {
            var notification = new CreateAuctionNotification(
                EventId: message.EventId,
                AuctionId: message.AuctionId,
                Name: message.Name,
                Status: message.Status,
                StartsAt: message.StartsAt,
                EndsAt: message.EndsAt,
                LotCount: message.LotCount,
                ItemCount: message.ItemCount,
                OccurredAt: message.OccurredAt);

            return hubContext.Clients.All.AuctionCreatedAsync(notification);
        }
    }
}
