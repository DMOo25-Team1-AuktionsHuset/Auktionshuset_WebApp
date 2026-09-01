using Auktionshuset.Domain.Entities.AuctionBusiness;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Auktionshuset.Domain.Entities.AuctionBusiness
{
    public class Auction
    {
        public required Guid AuctionId { get; set; }
        public required string Auctioneer { get; set; }
        public required DateOnly DateTime { get; set; }
        public required Employee Employee { get; set; }
        public required Guid EmployeeId { get; set; }
        
    }
}
