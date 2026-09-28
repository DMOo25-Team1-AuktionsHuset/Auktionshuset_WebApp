using System;
using System.Collections.Generic;

namespace Auktionshuset.Domain.Entities
{
    public class DeviceSession
    {
        public required Guid DeviceSessionId { get; set; }
        public required Guid CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;
        public required Guid AuctionId { get; set; }
        public Auction Auction { get; set; } = null!;
        public required Guid DeviceId { get; set; }
        public Device Device { get; set; } = null!;
        public required DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }

        public ICollection<Bid> Bids { get; set; } = new List<Bid>();
    }
}
