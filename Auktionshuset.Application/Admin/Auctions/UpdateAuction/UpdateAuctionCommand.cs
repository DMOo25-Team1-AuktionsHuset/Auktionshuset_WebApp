namespace Auktionshuset.Application.Admin.Auctions.UpdateAuction;

public sealed record UpdateAuctionCommand(
    Guid AuctionId,
    string Name,
    DateTime StartsAt,
    DateTime EndsAt,
    Guid EmployeeId,
    Guid? AuctionHouseId,
    IReadOnlyCollection<AuctionLotSelection> Lots);
