using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Domain.Entities.Auction
{
    public class Bid
    {
        public required Guid BidId { get; set; }
        public required DateTime BidDateTime { get; set; }
        public required decimal BidAmount { get; set; }
        public required bool IsWinningBid { get; set; } = false;
        public required AuctionLot AuctionLot { get; set; }
        public required Guid AuctionLotId { get; set; }
        public required Customer.Customer Customer { get; set; }
        public required Guid CustomerId { get; set; }
    }
}
