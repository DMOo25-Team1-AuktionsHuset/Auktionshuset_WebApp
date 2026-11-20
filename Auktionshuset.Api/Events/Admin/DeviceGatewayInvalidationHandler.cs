using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Application.Admin.Devices;
using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Api.Events.Admin;

// Notifications are invalidations: consumers must reload authoritative state and tolerate retries.
public interface IDeviceGatewayInvalidations
{
    Task AuctionChangedAsync(Guid eventId, Guid auctionId, long auctionRevision, CancellationToken cancellationToken);
    Task AssignmentChangedAsync(Guid eventId, Guid deviceId, Guid sessionId, CancellationToken cancellationToken);
}

public sealed class DeviceGatewayInvalidationHandler(IDeviceGatewayInvalidations gateway) :
    IIntegrationEventHandler<AuctionRevisionChangedIntegrationEvent>,
    IIntegrationEventHandler<DeviceIssuedIntegrationEvent>,
    IIntegrationEventHandler<DeviceReleasedIntegrationEvent>
{
    public Task HandleAsync(AuctionRevisionChangedIntegrationEvent message, CancellationToken cancellationToken) =>
        gateway.AuctionChangedAsync(message.EventId, message.AuctionId, message.AuctionRevision, cancellationToken);

    public Task HandleAsync(DeviceIssuedIntegrationEvent message, CancellationToken cancellationToken) =>
        gateway.AssignmentChangedAsync(message.EventId, message.DeviceId, message.SessionId, cancellationToken);

    public Task HandleAsync(DeviceReleasedIntegrationEvent message, CancellationToken cancellationToken) =>
        gateway.AssignmentChangedAsync(message.EventId, message.DeviceId, message.SessionId, cancellationToken);
}
