using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Application.Admin.Auctions.CreateAuction;

public sealed record AuctionCreatedIntegrationEvent(
    Guid EventId,
    Guid AuctionId,
    DateTime StartsAt,
    int LotCount,
    DateTime OccurredAt) : IIntegrationEvent;
