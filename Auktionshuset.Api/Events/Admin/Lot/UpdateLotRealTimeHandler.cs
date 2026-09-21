using Auktionshuset.Api.Hubs;
using Auktionshuset.Application.Admin.Lots.UpdateLot;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Contracts.Dto.Admin.Lot;
using Auktionshuset.Contracts.Dto.Admin.Lot.Image;
using Auktionshuset.Contracts.Dto.Admin.Lot.UpdateLot;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Events.Admin.Lot {
    public class UpdateLotRealTimeHandler(IHubContext<LotHub, ILotClient> hubContext) : IIntegrationEventHandler<LotUpdatedIntegrationEvent> {
        /// <summary>
        /// Broadcasts a lot-updated notification to all connected clients.
        /// </summary>
        /// <param name="message">The integration event describing the updated lot.</param>
        /// <returns>A task that completes once the notification has been broadcast.</returns>
        public Task HandleAsync(LotUpdatedIntegrationEvent message, CancellationToken cancellationToken) {
            var notification = new UpdateLotNotification(
                EventId: message.EventId,
                LotId: message.LotId,
                AuctionHouseId: message.AuctionHouseId,
                Name: message.Name,
                Category: message.Category,
                Quantity: message.Quantity,
                EstimatedValue: message.EstimatedValue,
                ImageUrl: LotImagePaths.ToUrl(message.ImageFileName),
                OccurredAt: message.OccurredAt);

            return hubContext.Clients.All.LotUpdatedAsync(notification);
        }
    }
}
