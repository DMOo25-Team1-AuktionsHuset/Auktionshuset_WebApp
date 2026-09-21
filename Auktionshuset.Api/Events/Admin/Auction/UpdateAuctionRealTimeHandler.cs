using Auktionshuset.Api.Hubs;
using Auktionshuset.Application.Admin.Auctions.UpdateAuction;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Contracts.Dto.Admin.Auction;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Events.Admin.Auction
{
    public class UpdateAuctionRealTimeHandler(IHubContext<AuctionHub, IAuctionClient> hubContext)
        : IIntegrationEventHandler<AuctionUpdatedIntegrationEvent>
    {
        /// <summary>
        /// Broadcasts an auction-updated notification to all connected clients.
        /// </summary>
        /// <param name="message">The integration event describing the updated auction.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>A task that completes once the notification has been broadcast.</returns>
        public Task HandleAsync(AuctionUpdatedIntegrationEvent message, CancellationToken cancellationToken)
        {
            var notification = new UpdateAuctionNotification(
                EventId: message.EventId,
                AuctionId: message.AuctionId,
                Name: message.Name,
                Status: message.Status,
                StartsAt: message.StartsAt,
                EndsAt: message.EndsAt,
                LotCount: message.LotCount,
                ItemCount: message.ItemCount,
                OccurredAt: message.OccurredAt);

            return hubContext.Clients.All.AuctionUpdatedAsync(notification);
        }
    }
}
