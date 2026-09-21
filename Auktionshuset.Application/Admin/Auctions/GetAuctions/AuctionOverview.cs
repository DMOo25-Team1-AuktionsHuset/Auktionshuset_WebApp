using Auktionshuset.Domain.Entities;
using AuctionEntity = Auktionshuset.Domain.Entities.Auction;

namespace Auktionshuset.Application.Admin.Auctions.GetAuctions;

/// <summary>
/// A dashboard row for an auction: the auction, its auctionarius and its lot totals.
/// </summary>
/// <param name="LotCount">The number of distinct lot lines on the auction.</param>
/// <param name="ItemCount">The total number of units across every lot line.</param>
/// <param name="ImageFileNames">The stored image file names of the lots on the auction.</param>
public sealed record AuctionOverview(
    AuctionEntity Auction,
    string EmployeeName,
    int LotCount,
    int ItemCount,
    IReadOnlyList<string> ImageFileNames);

/// <summary>
/// One lot line on an auction, including the number of units the auction includes.
/// </summary>
public sealed record AuctionLotLine(
    Guid LotId,
    string Name,
    string Category,
    int Quantity,
    decimal EstimatedValue,
    string? ImageFileName);

/// <summary>
/// The full detail view of a single auction.
/// </summary>
public sealed record AuctionDetail(
    AuctionEntity Auction,
    string EmployeeName,
    int LotCount,
    int ItemCount,
    IReadOnlyList<AuctionLotLine> Lots);
