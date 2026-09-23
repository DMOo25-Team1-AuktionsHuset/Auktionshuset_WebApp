using System;
using System.Collections.Generic;

namespace Auktionshuset.Domain.Entities
{
    public class AuctionLot
    {
        public required Guid AuctionLotId { get; set; }
        public required Guid AuctionId { get; set; }
        public Auction Auction { get; set; } = null!;
        public required Guid LotId { get; set; }
        public Lot Lot { get; set; } = null!;

        /// <summary>
        /// Number of units of the lot included in this auction. Kept because the application uses it
        /// for stock validation and auction totals.
        /// </summary>
        public required int Quantity { get; set; }

        public Guid? CurrentHighestBidId { get; set; }
        public Bid? CurrentHighestBid { get; set; }
        public bool OpenForBids { get; set; } = false;

        public ICollection<Bid> Bids { get; set; } = new List<Bid>();
        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    }
}
