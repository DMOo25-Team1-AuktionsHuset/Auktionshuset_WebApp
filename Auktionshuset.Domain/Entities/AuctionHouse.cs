using System;
using System.Collections.Generic;

namespace Auktionshuset.Domain.Entities
{
    public class AuctionHouse
    {
        public required Guid AuctionHouseId { get; set; }
        public required string AuctionHouseName { get; set; }
        public required string Address { get; set; }
        public required int CVRNumber { get; set; }
        public required string PhoneNumber { get; set; }
        public required string Email { get; set; }

        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
        public ICollection<Device> Devices { get; set; } = new List<Device>();
        public ICollection<Lot> Lots { get; set; } = new List<Lot>();
        public ICollection<Auction> Auctions { get; set; } = new List<Auction>();
    }
}
