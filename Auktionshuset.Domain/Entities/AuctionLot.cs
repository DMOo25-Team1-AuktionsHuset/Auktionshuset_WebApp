namespace Auktionshuset.Domain.Entities
{
    public class AuctionLot
    {
        public required Guid AuctionLotId { get; set; }

        public required Guid AuctionId { get; set; }
        public required Auction Auction { get; set; }

        public required Guid LotId { get; set; }
        public required Lot Lot { get; set; }

        /// <summary>
        /// Gets or sets the number of units of the lot that this auction includes. Always at least 1
        /// and never more than the lot's stock quantity.
        /// </summary>
        public required int Quantity { get; set; }

        public Bid? CurrentHighestBidId { get; set; }
    }
}
