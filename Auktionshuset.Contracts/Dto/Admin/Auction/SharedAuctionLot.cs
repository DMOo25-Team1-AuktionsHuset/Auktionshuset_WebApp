namespace Auktionshuset.Contracts.Dto.Admin.Auction;

public sealed record SelectSharedAuctionLotRequest(Guid? AuctionLotId);
public sealed record SharedAuctionLotResponse(Guid AuctionId, Guid? AuctionLotId, long AuctionRevision);
public sealed record SharedAuctionLotChangedNotification(Guid EventId, Guid AuctionId, Guid? AuctionLotId, long AuctionRevision);
