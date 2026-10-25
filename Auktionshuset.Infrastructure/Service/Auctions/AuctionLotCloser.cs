using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.Admin.Auctions.CloseAuction;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Infrastructure.Service.Auctions
{
    internal static class AuctionLotCloser
    {
        // The caller is responsible for the transaction and the auctionlock
        public static async Task<Guid?> StageAsync(
            AHDBContext context,
            IOutboxWriter outboxWriter,
            AuctionLot item,
            DateTime nowUtc,
            CancellationToken cancellationToken)
        {
            if (item.ClosedAtTime is not null) return item.CurrentHighestBidId;

            var winner = await context.Bids
                .Where(bid => bid.AuctionLotId == item.AuctionLotId)
                .OrderByDescending(bid => bid.Amount)
                .ThenBy(bid => bid.SequenceNumber)
                .Select(bid => new { bid.BidId, bid.Amount })
                .FirstOrDefaultAsync(cancellationToken);

            item.OpenForBids = false;
            item.ClosedAtTime = nowUtc;
            item.CurrentHighestBidId = winner?.BidId;

            await outboxWriter.AddAsync(
                new AuctionLotClosedIntegrationEvent(
                    EventId: Guid.NewGuid(),
                    AuctionId: item.AuctionId,
                    AuctionLotId: item.AuctionLotId,
                    WinningBidId: winner?.BidId,
                    WinningAmount: winner?.Amount,
                    OccurredAt: nowUtc),
                cancellationToken);

            return winner?.BidId;
        }
    }
}
