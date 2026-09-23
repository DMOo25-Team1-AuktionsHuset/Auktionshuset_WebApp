using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.Auction {
    public sealed record PlaceBidResult(
        bool Accepted, 
        bool Duplicate, 
        Guid? BidId, 
        int? SequenceNumber, 
        decimal CurrentBid, 
        string? ErrorCode);
}
