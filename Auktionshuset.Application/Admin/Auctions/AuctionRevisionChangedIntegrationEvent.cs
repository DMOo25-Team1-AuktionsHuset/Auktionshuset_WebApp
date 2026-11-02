using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Application.Admin.Auctions;

public sealed record AuctionRevisionChangedIntegrationEvent(
    Guid EventId, Guid AuctionId, Guid? AuctionLotId, long AuctionRevision, DateTime OccurredAt) : IIntegrationEvent;
