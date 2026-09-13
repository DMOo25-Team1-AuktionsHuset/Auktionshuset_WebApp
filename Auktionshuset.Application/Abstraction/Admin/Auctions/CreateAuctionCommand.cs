namespace Auktionshuset.Application.Abstraction.Admin.Auctions;

public sealed record CreateAuctionCommand(
    DateTime StartsAt,
    IReadOnlyCollection<Guid> LotIds);
