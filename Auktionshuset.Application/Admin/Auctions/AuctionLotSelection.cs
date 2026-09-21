namespace Auktionshuset.Application.Admin.Auctions;

/// <summary>
/// A lot chosen for an auction together with the number of units the auction includes.
/// </summary>
public sealed record AuctionLotSelection(Guid LotId, int Quantity);
