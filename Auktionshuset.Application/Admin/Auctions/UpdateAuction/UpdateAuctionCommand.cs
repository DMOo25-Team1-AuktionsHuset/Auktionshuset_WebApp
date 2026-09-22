namespace Auktionshuset.Application.Admin.Auctions.UpdateAuction;

/// <summary>
/// The values that replace an existing auction.
/// </summary>
/// <param name="EmployeeId">
/// The identifier of the auctionarius, or <see langword="null"/> to save the auction without an
/// employee. An auctionarius that was previously assigned can be removed this way.
/// </param>
public sealed record UpdateAuctionCommand(
    Guid AuctionId,
    string Name,
    DateTime StartsAt,
    DateTime EndsAt,
    Guid? EmployeeId,
    Guid? AuctionHouseId,
    IReadOnlyCollection<AuctionLotSelection> Lots);
