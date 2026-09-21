namespace Auktionshuset.Contracts.Dto.Admin.Auction {
    /// <summary>
    /// One selected lot together with the number of units that the auction includes.
    /// </summary>
    public sealed record AuctionLotRequest(Guid LotId, int Quantity);
}
