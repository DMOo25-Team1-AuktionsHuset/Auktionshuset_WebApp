using System;
using System.Collections.Generic;

namespace Auktionshuset.Domain.Entities
{
    public class Bid
    {
        public required Guid BidId { get; set; }
        public required Guid AuctionLotId { get; set; }
        public AuctionLot AuctionLot { get; set; } = null!;
        public required Guid DeviceSessionId { get; set; }
        public DeviceSession DeviceSession { get; set; } = null!;
        public required decimal Amount { get; set; }
        public required DateTime PlacedAt { get; set; }
        public required int SequenceNumber { get; set; }

        public ICollection<AuctionLot> CurrentHighestForAuctionLots { get; set; } = new List<AuctionLot>();
        public ICollection<Invoice> WinningInvoices { get; set; } = new List<Invoice>();
    }
}
