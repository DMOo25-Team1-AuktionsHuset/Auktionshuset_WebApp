namespace Auktionshuset.Application.Admin.Auctions.CreateAuction;

public sealed record CreateAuctionCommand(
    DateTime StartsAt,
    IReadOnlyCollection<Guid> LotIds);
