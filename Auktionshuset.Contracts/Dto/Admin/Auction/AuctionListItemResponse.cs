namespace Auktionshuset.Contracts.Dto.Admin.Auction;

/// <summary>
/// A row in the auction dashboard.
/// </summary>
/// <param name="EmployeeId">The identifier of the required auctionarius.</param>
/// <param name="LotCount">The number of distinct lot lines on the auction.</param>
/// <param name="ItemCount">The total number of units across every lot line.</param>
/// <param name="ImageUrls">Relative image URLs for the lots on the auction, used for the thumbnails.</param>
public sealed record AuctionListItemResponse(
    Guid AuctionId,
    string Name,
    string Status,
    DateTime StartsAt,
    DateTime? EndsAt,
    Guid? EmployeeId,
    string EmployeeName,
    int LotCount,
    int ItemCount,
    IReadOnlyList<string> ImageUrls);
