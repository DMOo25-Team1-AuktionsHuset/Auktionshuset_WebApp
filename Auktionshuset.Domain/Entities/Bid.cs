using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Domain.Entities {
    public class Bid {
        public required Guid BidId { get; set; }
        public required decimal Amount { get; set; }
        public required DateTime PlacetAt { get; set; }
        public required int SequenceNumber { get; set; }

        public required AuctionLot AuctionLotId { get; set; }
        public required DeviceSession DeviceSessionId { get; set; }
    }
}
