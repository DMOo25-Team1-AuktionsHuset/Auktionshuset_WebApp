using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Domain.Entities {
    public class DeviceSession {
        public required Guid DeviceSessionId { get; set; }
        public required DateTime StartedAt { get; set; }
        public required DateTime EndedAt { get; set; }

        public required Customer CustomerId { get; set; }
        public required Auction AuctionId { get; set; }
        public required Device DeviceId { get; set; }
    }
}
