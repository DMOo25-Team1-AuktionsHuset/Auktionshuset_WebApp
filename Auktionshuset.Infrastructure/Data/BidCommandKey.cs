using Auktionshuset.Application.Auction;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Infrastructure.Data {
    public class BidCommandKey {
        public Guid BidCommandKeyId { get; set; }

        public required Guid CustomerId { get; set; }
        public required Guid? DeviceSessionId { get; set; }
        public required Guid RequestId { get; set; }
        public required Guid AuctionLotId { get; set; }
        public required decimal RequestedAmount { get; set; }

        public required bool Accepted { get; set; }
        public required Guid? BidId { get; set; }
        public int? SequenceNumber { get; set; }

        public required decimal CurrentPrice { get; set; }
        public string? ErrorCode { get; set; }
        public required DateTime ProcessedAt { get; set; }

        public bool Matches(Guid auctionLotId, decimal amount) => 
            AuctionLotId == auctionLotId && RequestedAmount == amount;

        public PlaceBidResult ToResult(bool duplicate) =>
            new(Accepted, duplicate, BidId, SequenceNumber, CurrentPrice, ErrorCode);
    }
}
