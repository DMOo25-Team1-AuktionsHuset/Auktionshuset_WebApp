namespace Auktionshuset.Application.Admin.Auctions;

/// <summary>
/// Units sold together to one winner. StartingPrice is a line total; null preserves an existing price
/// or defaults to zero for a new line.
/// </summary>
public sealed record AuctionLotSelection(Guid LotId, int Quantity, decimal? StartingPrice = null);
