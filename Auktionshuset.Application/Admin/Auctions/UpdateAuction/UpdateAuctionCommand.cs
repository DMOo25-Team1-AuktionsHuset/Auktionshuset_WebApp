namespace Auktionshuset.Application.Admin.Auctions.UpdateAuction;

/// <summary>
/// The values that replace an existing auction.
/// </summary>
/// <param name="EmployeeId">
/// The identifier of the optional auctionarius assigned to the auction.
/// </param>
public sealed record UpdateAuctionCommand(
    Guid AuctionId,
    string Name,
    DateTime StartsAt,
    DateTime EndsAt,
    Guid? EmployeeId,
    Guid? AuctionHouseId,
    IReadOnlyCollection<AuctionLotSelection> Lots);
