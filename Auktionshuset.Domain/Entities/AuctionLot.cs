using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Domain.Entities {
    public class AuctionLot {
        public required Guid AuctionLotId { get; set; }

        public required Auction AuctionId { get; set; }
        public required Lot LotId { get; set; }
        public Bid? CurrentHighestBidId { get; set; }
    }
}
