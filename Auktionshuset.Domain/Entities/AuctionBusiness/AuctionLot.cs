using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Auktionshuset.Domain.Entities.AuctionBusiness
{
    public class AuctionLot
    { public required Auction Auction { get; set; }
        public required Guid AuctionId { get; set; }
        public required Lot Lot { get; set; }
        public required Guid LotId { get; set; }
    }
}
