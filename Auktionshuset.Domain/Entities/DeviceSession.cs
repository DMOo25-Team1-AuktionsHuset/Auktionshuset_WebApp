using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Domain.Entities {
    public class DeviceSession {
        public required Guid DeviceSessionId { get; set; }
        public required DateTime StartedAt { get; set; }
        public required DateTime? EndedAt { get; set; }

        public required Guid CustomerId { get; set; }
        public required Customer Customer { get; set; }

        public required Guid AuctionId { get; set; }
        public required Auction Auction { get; set; }

        public required Guid DeviceId { get; set; }
        public required Device Device { get; set; }
    }
}
