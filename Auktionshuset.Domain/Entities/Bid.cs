using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Domain.Entities {
    public class Bid {
        public required Guid BidId { get; set; }
        public required decimal Amount { get; set; }
        public required DateTime PlacedAt { get; set; }
        public required int SequenceNumber { get; set; }

        public required Guid AuctionLotId { get; set; }
        public required AuctionLot AuctionLot { get; set; }

        public required Guid? DeviceSessionId { get; set; }
        public required DeviceSession? DeviceSession { get; set; }

        public required Guid CustomerId { get; set; }
        public required Customer Customer { get; set; }
    }
}
