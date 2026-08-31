using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Auktionshuset.Domain.Entities.Auction
{
    public class AuctionLot
    {
        public required Guid AuctionId { get; set; }
        public required Guid LotID { get; set; }

        public required Auction Auction { get; set; }
        public required Lot Lot { get; set; }
    }
}
