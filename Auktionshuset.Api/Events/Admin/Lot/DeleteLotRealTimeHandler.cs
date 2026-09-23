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
        /// <summary>
        /// Broadcasts a lot-deleted notification to all connected clients.
        /// </summary>
        /// <param name="message">The integration event describing the deleted lot.</param>
        /// <returns>A task that completes once the notification has been broadcast.</returns>
        public Task HandleAsync(LotDeletedIntegrationEvent message, CancellationToken cancellationToken)
        {
            DeleteLotNotification notification = new DeleteLotNotification(
                EventId: message.EventId,
                LotId: message.LotId,
                OccurredAt: message.OccurredAt);

            return hubContext.Clients.All.LotDeletedAsync(notification);
        }
    }

}
