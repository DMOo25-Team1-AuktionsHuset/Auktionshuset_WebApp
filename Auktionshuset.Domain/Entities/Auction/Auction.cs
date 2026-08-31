using Auktionshuset.Domain.Entities.Admin;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Auktionshuset.Domain.Entities.Auction
{
    public class Auction
    {
        public required Guid AuctionId { get; set; }
        public required string Auctioneer { get; set; }
        public required DateOnly DateTime { get; set; }
        public required Guid EmployeeId { get; set; }

        public required Employee Employee { get; set; }
    }
}
