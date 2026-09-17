using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Domain.Entities
{
    public class Auction
    {
        public required Guid AuctionId { get; set; }

        // Identity and auction house attribution are added together with access control later.
        public AuctionHouse? AuctionHouseId { get; set; }
        public Employee? EmployeeId { get; set; }

        public required DateTime StartsAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public required string AuctionStatus { get; set; }
    }
}
