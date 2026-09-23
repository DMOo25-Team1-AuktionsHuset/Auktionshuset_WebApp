using System;
using System.Collections.Generic;

namespace Auktionshuset.Domain.Entities
{
    public class Device
    {
        public required Guid DeviceId { get; set; }
        public required Guid AuctionHouseId { get; set; }
        public AuctionHouse AuctionHouse { get; set; } = null!;
        public required int DeviceNumber { get; set; }
        public required string Status { get; set; }

        public ICollection<DeviceSession> DeviceSessions { get; set; } = new List<DeviceSession>();
    }
}
