using Auktionshuset.Contracts.Dto.Admin.Lot.CreateLot;
using Auktionshuset.Api.Hubs;
using Auktionshuset.Application.Admin.Lots.CreateLot;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Contracts.Dto.Admin.Lot;
using Auktionshuset.Contracts.Dto.Admin.Lot.Image;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Events.Admin.Lot
{
    public class CreateLotRealTimeHandler(IHubContext<LotHub, ILotClient> hubContext) : IIntegrationEventHandler<LotCreatedIntegrationEvent>
    {
        /// <summary>
        /// Broadcasts a lot-created notification to all connected clients.
        /// </summary>
        /// <param name="message">The integration event describing the created lot.</param>
        /// <returns>A task that completes once the notification has been broadcast.</returns>
        public Task HandleAsync(LotCreatedIntegrationEvent message, CancellationToken cancellationToken)
        {
            var notification = new CreateLotNotification(
                EventId: message.EventId,
                LotId: message.LotId,
                AuctionHouseId: message.AuctionHouseId,
                Name: message.Name,
                Category: message.Category,
                Quantity: message.Quantity,
                EstimatedValue: message.EstimatedValue,
                ImageUrl: LotImagePaths.ToUrl(message.ImageFileName),
                OccurredAt: message.OccurredAt);

            return hubContext.Clients.All.LotCreatedAsync(notification);
        }
    }
}

