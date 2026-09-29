using Auktionshuset.Application.EventHandling;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.Admin.Auctions.Bids
{
    public sealed record BidPlacedIntegrationEvent(
        Guid EventId,
        Guid BidId,
        Guid AuctionId,
        Guid AuctionLotId,
        Guid CustomerId,
        decimal Amount,
        int SequenceNumber,
        DateTime OccurredAt) : IIntegrationEvent;
}
