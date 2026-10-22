using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Application.Admin.Auctions.CloseAuction
{
    public sealed record AuctionLotClosedIntegrationEvent(
        Guid EventId,
        Guid AuctionId,
        Guid AuctionLotId,
        Guid? WinningBidId,
        decimal? WinningAmount,
        DateTime OccurredAt) : IIntegrationEvent;

}
