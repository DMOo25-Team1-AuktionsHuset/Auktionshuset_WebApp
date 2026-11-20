using Auktionshuset.Api.Hubs;
using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Contracts.Dto.Admin.Auction;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Events.Admin.Auction;

public sealed class AuctionRevisionChangedRealTimeHandler(IHubContext<AuctionHub, IAuctionClient> hub)
    : IIntegrationEventHandler<AuctionRevisionChangedIntegrationEvent>
{
    public Task HandleAsync(AuctionRevisionChangedIntegrationEvent message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return hub.Clients.All.AuctionRevisionChangedAsync(new SharedAuctionLotChangedNotification(
            message.EventId, message.AuctionId, message.AuctionLotId, message.AuctionRevision));
    }
}
