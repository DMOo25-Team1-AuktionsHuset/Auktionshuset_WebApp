namespace Auktionshuset.Application.Admin.Auctions.CreateAuction;

/// <summary>
/// The values needed to create an auction.
/// </summary>
/// <param name="EmployeeId">
/// The identifier of the required auctionarius assigned to the auction.
/// </param>
public sealed record CreateAuctionCommand(
    string Name,
    DateTime StartsAt,
    DateTime EndsAt,
    Guid? EmployeeId,
    Guid? AuctionHouseId,
    IReadOnlyCollection<AuctionLotSelection> Lots);
