using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Application.Admin.Devices;

public sealed record DeviceRegisteredIntegrationEvent(
    Guid EventId, Guid DeviceId, Guid AuctionHouseId, int DeviceNumber, DateTime OccurredAt) : IIntegrationEvent;

public sealed record DeviceIssuedIntegrationEvent(
    Guid EventId, Guid DeviceId, Guid SessionId, Guid AuctionId, Guid CustomerId, DateTime OccurredAt) : IIntegrationEvent;

public sealed record DeviceReleasedIntegrationEvent(
    Guid EventId, Guid DeviceId, Guid SessionId, Guid AuctionId, Guid CustomerId, DateTime OccurredAt) : IIntegrationEvent;
