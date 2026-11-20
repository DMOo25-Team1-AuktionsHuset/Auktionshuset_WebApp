using Auktionshuset.Api.Events.Admin;
using Auktionshuset.Api.Services;
using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Application.Admin.Devices;
using Auktionshuset.Application.EventHandling;
using Microsoft.Extensions.DependencyInjection;

namespace Auktionshuset.Api.Test.Admins.Auctions;

public sealed class DeviceGatewayInvalidationTests
{
    [Fact]
    public async Task RegisteredHandlersForwardOnlyInvalidationIdentifiers()
    {
        var services = new ServiceCollection();
        services.AddApiServices();
        Assert.Contains(services, d => d.ServiceType == typeof(IDeviceGatewayInvalidations) && d.ImplementationType == typeof(DeviceGatewayHubInvalidations));
        Assert.Contains(services, d => d.ServiceType == typeof(IIntegrationEventHandler<AuctionRevisionChangedIntegrationEvent>) && d.ImplementationType == typeof(DeviceGatewayInvalidationHandler));
        Assert.Contains(services, d => d.ServiceType == typeof(IIntegrationEventHandler<DeviceIssuedIntegrationEvent>) && d.ImplementationType == typeof(DeviceGatewayInvalidationHandler));
        Assert.Contains(services, d => d.ServiceType == typeof(IIntegrationEventHandler<DeviceReleasedIntegrationEvent>) && d.ImplementationType == typeof(DeviceGatewayInvalidationHandler));

        var gateway = new RecordingGateway();
        var handler = new DeviceGatewayInvalidationHandler(gateway);
        var eventId = Guid.NewGuid();
        var auctionId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        await handler.HandleAsync(new AuctionRevisionChangedIntegrationEvent(eventId, auctionId, Guid.NewGuid(), 42, DateTime.UtcNow), cancellation.Token);
        Assert.Equal((eventId, auctionId, 42L, cancellation.Token), gateway.Auction);
        await handler.HandleAsync(new DeviceIssuedIntegrationEvent(eventId, deviceId, sessionId, auctionId, customerId, DateTime.UtcNow), cancellation.Token);
        Assert.Equal((eventId, deviceId, sessionId, cancellation.Token), gateway.Assignment);
        gateway.Assignment = null;
        await handler.HandleAsync(new DeviceReleasedIntegrationEvent(eventId, deviceId, sessionId, auctionId, customerId, DateTime.UtcNow), cancellation.Token);
        Assert.Equal((eventId, deviceId, sessionId, cancellation.Token), gateway.Assignment);
    }

    private sealed class RecordingGateway : IDeviceGatewayInvalidations
    {
        public (Guid, Guid, long, CancellationToken)? Auction { get; private set; }
        public (Guid, Guid, Guid, CancellationToken)? Assignment { get; set; }
        public Task AuctionChangedAsync(Guid eventId, Guid auctionId, long auctionRevision, CancellationToken cancellationToken)
        {
            Auction = (eventId, auctionId, auctionRevision, cancellationToken);
            return Task.CompletedTask;
        }
        public Task AssignmentChangedAsync(Guid eventId, Guid deviceId, Guid sessionId, CancellationToken cancellationToken)
        {
            Assignment = (eventId, deviceId, sessionId, cancellationToken);
            return Task.CompletedTask;
        }
    }
}
