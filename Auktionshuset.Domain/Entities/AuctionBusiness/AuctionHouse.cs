using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace Auktionshuset.Domain.Entities.AuctionBusiness
{
    public class AuctionHouse
    {
        public required Guid AuctionHouseId { get; set; }
        public required string AuctionHouseName { get; set; }
        public required string Address { get; set; }
        public required int CVRNumber { get; set; }
        public required string PhoneNumber { get; set; }
        public required string Email { get; set; }
    }
}
