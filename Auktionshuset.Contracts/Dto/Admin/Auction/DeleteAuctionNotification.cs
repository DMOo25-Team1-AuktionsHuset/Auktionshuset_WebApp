using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Contracts.Dto.Admin.Auction
{
    public sealed record DeleteAuctionNotification(
        Guid EventId,
        Guid AuctionId,
        DateTime OccurredAt);
}
