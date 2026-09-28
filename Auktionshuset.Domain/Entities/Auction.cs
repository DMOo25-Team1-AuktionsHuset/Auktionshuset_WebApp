using System;
using System.Collections.Generic;

namespace Auktionshuset.Domain.Entities
{
    public class Auction
    {
        public required Guid AuctionId { get; set; }
        public required Guid AuctionHouseId { get; set; }
        public AuctionHouse AuctionHouse { get; set; } = null!;
        public required Guid EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;
        public required string Name { get; set; }
        public required DateTime StartsAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public required string AuctionStatus { get; set; }

        public ICollection<AuctionLot> AuctionLots { get; set; } = new List<AuctionLot>();
        public ICollection<DeviceSession> DeviceSessions { get; set; } = new List<DeviceSession>();
    }
}
