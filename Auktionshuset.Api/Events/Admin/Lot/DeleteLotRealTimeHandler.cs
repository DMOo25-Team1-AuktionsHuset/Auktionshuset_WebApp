using Auktionshuset.Api.Hubs;
using Auktionshuset.Application.Admin.Lots.DeleteLot;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Contracts.Dto.Admin.Lot;
using Auktionshuset.Contracts.Dto.Admin.Lot.DeleteLot;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Events.Admin.Lot
{
    public class DeleteLotRealTimeHandler(
        IHubContext<LotHub, ILotClient> hubContext)
        : IIntegrationEventHandler<LotDeletedIntegrationEvent>
    {
        public Task HandleAsync(LotDeletedIntegrationEvent message, CancellationToken cancellationToken)
        {
            var notification = new DeleteLotNotification(
                LotId: message.LotId);

            return hubContext.Clients.All.LotDeletedAsync(notification);
        }
    }

}
