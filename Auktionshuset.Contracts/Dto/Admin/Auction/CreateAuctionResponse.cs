namespace Auktionshuset.Contracts.Dto.Admin.Auction;

public sealed record CreateAuctionResponse(Guid AuctionId, int LotCount, int ItemCount);

public sealed record UpdateAuctionResponse(Guid AuctionId, int LotCount, int ItemCount);
