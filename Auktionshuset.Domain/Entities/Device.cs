using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Domain.Entities {
    public class Device {
        public required Guid DeviceId { get; set; }
        public required int DeviceNumber { get; set; }
        public required string Status { get; set; }

        public AuctionHouse AuctionHouseId { get; set; }
    }
}
