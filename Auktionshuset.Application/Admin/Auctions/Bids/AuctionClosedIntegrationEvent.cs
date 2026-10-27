using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Application.Admin.Auctions.Bids
{
    public sealed record AuctionClosedIntegrationEvent(
        Guid EventId,
        Guid AuctionId,
        Guid AuctionLotId,
        Guid? WinningBidId,
        DateTime OccurredAt) : IIntegrationEvent;
}
