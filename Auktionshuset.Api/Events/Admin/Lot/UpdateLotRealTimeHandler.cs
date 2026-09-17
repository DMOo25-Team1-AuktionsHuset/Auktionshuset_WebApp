using Auktionshuset.Api.Hubs;
using Auktionshuset.Application.Admin.Lots.UpdateLot;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Contracts.Dto.Admin.Lot;
using Auktionshuset.Contracts.Dto.Admin.Lot.UpdateLot;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Events.Admin.Lot {
    public class UpdateLotRealTimeHandler(IHubContext<LotHub, ILotClient> hubContext) : IIntegrationEventHandler<LotUpdatedIntegrationEvent> {
        public Task HandleAsync(LotUpdatedIntegrationEvent message, CancellationToken cancellationToken) {
            var notification = new UpdateLotNotification(
                EventId: message.EventId,
                LotId: message.LotId,
                AuctionHouseId: message.AuctionHouseId,
                Name: message.Name,
                Category: message.Category,
                Quantity: message.Quantity,
                EstimatedValue: message.EstimatedValue,
                OccurredAt: message.OccurredAt);

            return hubContext.Clients.All.LotUpdatedAsync(notification);
        }
    }
}
