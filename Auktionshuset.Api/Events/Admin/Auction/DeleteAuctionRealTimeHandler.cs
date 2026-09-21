using Auktionshuset.Api.Hubs;
using Auktionshuset.Application.Admin.Auctions.DeleteAuction;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Contracts.Dto.Admin.Auction;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Events.Admin.Auction
{
    public class DeleteAuctionRealTimeHandler(IHubContext<AuctionHub, IAuctionClient> hubContext)
        : IIntegrationEventHandler<AuctionDeletedIntegrationEvent>
    {
        /// <summary>
        /// Broadcasts an auction-deleted notification to all connected clients.
        /// </summary>
        /// <param name="message">The integration event describing the deleted auction.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>A task that completes once the notification has been broadcast.</returns>
        public Task HandleAsync(AuctionDeletedIntegrationEvent message, CancellationToken cancellationToken)
        {
            var notification = new DeleteAuctionNotification(
                EventId: message.EventId,
                AuctionId: message.AuctionId,
                OccurredAt: message.OccurredAt);

            return hubContext.Clients.All.AuctionDeletedAsync(notification);
        }
    }
}
