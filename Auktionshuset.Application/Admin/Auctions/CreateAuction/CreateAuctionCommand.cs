namespace Auktionshuset.Application.Admin.Auctions.CreateAuction;

/// <summary>
/// The values needed to create an auction.
/// </summary>
/// <param name="EmployeeId">
/// The identifier of the auctionarius, or <see langword="null"/> when the auction is created
/// without an employee.
/// </param>
public sealed record CreateAuctionCommand(
    string Name,
    DateTime StartsAt,
    DateTime EndsAt,
    Guid? EmployeeId,
    Guid? AuctionHouseId,
    IReadOnlyCollection<AuctionLotSelection> Lots);
