using System.Runtime.InteropServices.JavaScript;

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
        /// Units sold together as one line to one winner. Open lines on upcoming/live auctions
        /// reserve this quantity; a sold close debits it once from physical stock.
        /// </summary>
        public required int Quantity { get; set; }
        public bool OpenForBids { get; set; } = false;

        /// <summary>Total starting price for the whole line, never a per-unit price. Zero is valid.</summary>
        public decimal StartingPrice { get; set; }

        public DateTime? ClosedAtTime { get; set; }
        public Guid? CurrentHighestBidId { get; set; }
        public Bid? CurrentHighestBid { get; set; }
        public ICollection<Bid> Bids { get; set; } = new List<Bid>();
        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    }
}
