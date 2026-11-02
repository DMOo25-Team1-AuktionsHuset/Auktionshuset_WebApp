using Auktionshuset.Application.Admin.Devices;
using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Api.Events.Admin;

public sealed class DeviceEventAuditHandler(ILogger<DeviceEventAuditHandler> logger) :
    IIntegrationEventHandler<DeviceRegisteredIntegrationEvent>,
    IIntegrationEventHandler<DeviceIssuedIntegrationEvent>,
    IIntegrationEventHandler<DeviceReleasedIntegrationEvent>
{
    public Task HandleAsync(DeviceRegisteredIntegrationEvent message, CancellationToken cancellationToken) =>
        LogAsync("registered", message.EventId, message.DeviceId, cancellationToken);

    public Task HandleAsync(DeviceIssuedIntegrationEvent message, CancellationToken cancellationToken) =>
        LogAsync("issued", message.EventId, message.DeviceId, cancellationToken);

    public Task HandleAsync(DeviceReleasedIntegrationEvent message, CancellationToken cancellationToken) =>
        LogAsync("released", message.EventId, message.DeviceId, cancellationToken);

    private Task LogAsync(string action, Guid eventId, Guid deviceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Device {DeviceId} {Action}; event {EventId}", deviceId, action, eventId);
        return Task.CompletedTask;
    }
}
