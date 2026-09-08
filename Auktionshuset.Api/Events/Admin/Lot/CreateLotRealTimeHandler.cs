using Auktionshuset.Api.Hubs;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Contracts.Dto.Admin.Lot;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Events.Admin.Lot {
    public class CreateLotRealTimeHandler(IHubContext<LotHub, ILotClient> hubContext) : IIntegrationEventHandler<CreateLotIntegrationEvent> {
        public Task HandleAsync(CreateLotIntegrationEvent message, CancellationToken cancellationToken) {
            var notification = new CreateLotNotification(
                EventId: message.EventId,
                LotId: message.LotId,
                AuctionHouseId: message.AuctionHouseId,
                Name: message.Name,
                Category: message.Category,
                Quantity: message.Quantity,
                EstimatedValue: message.EstimatedValue,
                OccurredAt: message.OccurredAt);

            return hubContext.Clients.All.LotCreatedAsync(notification);
        }
    }
}
