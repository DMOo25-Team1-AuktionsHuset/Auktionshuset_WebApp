using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Application.Admin.Auctions.UpdateAuction;

public sealed record AuctionUpdatedIntegrationEvent(
    Guid EventId,
    Guid AuctionId,
    string Name,
    string Status,
    DateTime StartsAt,
    DateTime EndsAt,
    int LotCount,
    int ItemCount,
    DateTime OccurredAt) : IIntegrationEvent;
