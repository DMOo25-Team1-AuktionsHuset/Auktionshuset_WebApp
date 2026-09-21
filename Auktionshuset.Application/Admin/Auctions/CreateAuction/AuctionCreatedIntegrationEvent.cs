using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Application.Admin.Auctions.CreateAuction;

public sealed record AuctionCreatedIntegrationEvent(
    Guid EventId,
    Guid AuctionId,
    string Name,
    string Status,
    DateTime StartsAt,
    DateTime EndsAt,
    int LotCount,
    int ItemCount,
    DateTime OccurredAt) : IIntegrationEvent;
