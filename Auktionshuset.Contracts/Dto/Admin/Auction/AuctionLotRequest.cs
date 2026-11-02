namespace Auktionshuset.Contracts.Dto.Admin.Auction
{
    /// <summary>
    /// Units sold together to one winner; StartingPrice is the total price for the line.
    /// Omitted price defaults to zero on new lines and preserves the price on existing lines.
    /// </summary>
    public sealed record AuctionLotRequest(Guid LotId, int Quantity, decimal? StartingPrice = null);
}
