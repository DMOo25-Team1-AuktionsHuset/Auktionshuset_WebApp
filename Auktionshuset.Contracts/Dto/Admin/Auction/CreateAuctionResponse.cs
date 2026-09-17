namespace Auktionshuset.Contracts.Dto.Admin.Auction;

public sealed record CreateAuctionResponse(Guid AuctionId, int LotCount);
