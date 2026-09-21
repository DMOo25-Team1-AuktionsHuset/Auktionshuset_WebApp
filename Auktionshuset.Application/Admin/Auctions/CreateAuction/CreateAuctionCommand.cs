namespace Auktionshuset.Application.Admin.Auctions.CreateAuction;

public sealed record CreateAuctionCommand(
    string Name,
    DateTime StartsAt,
    DateTime EndsAt,
    Guid EmployeeId,
    Guid? AuctionHouseId,
    IReadOnlyCollection<AuctionLotSelection> Lots);
