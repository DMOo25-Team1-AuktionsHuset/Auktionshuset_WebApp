using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Application.Abstraction.Admin.Auctions;

public sealed record AuctionCreatedIntegrationEvent(
    Guid EventId,
    Guid AuctionId,
    DateTime StartsAt,
    int LotCount,
    DateTime OccurredAt) : IIntegrationEvent;
