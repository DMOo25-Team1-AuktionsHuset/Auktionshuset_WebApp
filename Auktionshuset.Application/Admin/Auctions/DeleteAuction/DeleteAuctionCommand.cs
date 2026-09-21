using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Application.Admin.Auctions.DeleteAuction;

public sealed record DeleteAuctionCommand(Guid AuctionId);

public sealed record AuctionDeletedIntegrationEvent(
    Guid EventId,
    Guid AuctionId,
    DateTime OccurredAt) : IIntegrationEvent;
