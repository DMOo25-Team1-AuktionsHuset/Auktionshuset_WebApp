using Auktionshuset.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Events.Admin;

public sealed class DeviceGatewayHubInvalidations(IHubContext<DeviceGatewayHub, IDeviceGatewayClient> hub)
    : IDeviceGatewayInvalidations
{
    public Task AuctionChangedAsync(Guid eventId, Guid auctionId, long auctionRevision, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return hub.Clients.All.AuctionChanged(eventId, auctionId, auctionRevision);
    }

    public Task AssignmentChangedAsync(Guid eventId, Guid deviceId, Guid sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return hub.Clients.All.AssignmentChanged(eventId, deviceId, sessionId);
    }
}
