using Auktionshuset.Application.Auction;

namespace Auktionshuset.Infrastructure.Data
{
    public class BidCommandKey
    {
        public Guid BidCommandKeyId { get; set; }

        public required Guid CustomerId { get; set; }
        public required Guid? DeviceSessionId { get; set; }
        public required Guid RequestId { get; set; }
        public required Guid AuctionLotId { get; set; }
        public required decimal RequestedAmount { get; set; }
        public long? AuctionRevision { get; set; }

        public required bool Accepted { get; set; }
        public required Guid? BidId { get; set; }
        public int? SequenceNumber { get; set; }

        public required decimal CurrentPrice { get; set; }
        public string? ErrorCode { get; set; }
                public long? ResultAuctionRevision { get; set; }
        public required DateTime ProcessedAt { get; set; }

        public bool Matches(PlaceBidCommand command) =>
            AuctionLotId == command.AuctionLotId && RequestedAmount == command.Amount
            && DeviceSessionId == command.DeviceSessionId
            && AuctionRevision == (command.DeviceSessionId is null ? null : command.AuctionRevision);

        public PlaceBidResult ToResult(bool duplicate) =>
            new(Accepted, duplicate, BidId, SequenceNumber, CurrentPrice, ErrorCode, ResultAuctionRevision);
    }
}
