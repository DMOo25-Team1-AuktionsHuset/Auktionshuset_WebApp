using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Domain.Entities.AuctionBusiness;
using Auktionshuset.Domain.Entities.Bid;

namespace Auktionshuset.Domain.Entities.AuctionHistory
{
    public class AuctionHistory
    {
        public required Bid.Bid Bid { get; set; }
        public required Guid BidId { get; set; }
        public required DateTime BidDateTime { get; set; }
        public required int BidAmount { get; set; }
        public required AuctionLot AuctionLot { get; set; }
        public required Guid AuctionLotId { get; set; }
        public required AuctionBusiness.Customer Customer { get; set; }
        public required Guid CustomerId { get; set; }
    }
}
