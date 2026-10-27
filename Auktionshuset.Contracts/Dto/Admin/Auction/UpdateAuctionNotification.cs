using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Contracts.Dto.Admin.Auction
{
    public sealed record UpdateAuctionNotification(
        Guid EventId,
        Guid AuctionId,
        string Name,
        string Status,
        DateTime StartsAt,
        DateTime? EndsAt,
        int LotCount,
        int ItemCount,
        DateTime OccurredAt);
}
