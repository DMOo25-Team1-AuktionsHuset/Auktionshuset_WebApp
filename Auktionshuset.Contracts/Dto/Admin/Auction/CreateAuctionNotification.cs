using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Contracts.Dto.Admin.Auction {
    public sealed record CreateAuctionNotification(
        Guid EventId,
        Guid AuctionId,
        DateTime StartsAt,
        int LotCount,
        DateTime OccurredAt);
}
