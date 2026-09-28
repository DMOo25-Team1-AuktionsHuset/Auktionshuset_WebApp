using System;
using System.Collections.Generic;

namespace Auktionshuset.Domain.Entities
{
    public class Employee
    {
        public required Guid EmployeeId { get; set; }
        public required Guid AuctionHouseId { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required DateOnly BirthDate { get; set; }
        public required string Address { get; set; }
        public AuctionHouse AuctionHouse { get; set; } = null!;

        public ICollection<Auction> Auctions { get; set; } = new List<Auction>();
    }
}
