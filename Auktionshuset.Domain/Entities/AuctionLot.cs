using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Domain.Entities {
    public class AuctionLot {
        public required Guid AuctionLotId { get; set; }

        public required Guid AuctionId { get; set; }
        public required Auction Auction { get; set; }

        public required Guid LotId { get; set; }
        public required Lot Lot { get; set; }

        public required int Quantity { get; set; }
        public required bool OpenForBids { get; set; }
        
        public decimal StartingPrice { get; set; }

        public required Guid? CurrentHighestBid { get; set; }
        public Bid? CurrentHighestBidId { get; set; }
    }
}
