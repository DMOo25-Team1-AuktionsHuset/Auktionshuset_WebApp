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

            if (winner is not null)
            {
                var stock = await context.Lot.SingleAsync(lot => lot.LotId == item.LotId, cancellationToken);
                var reservations = await new Auktionshuset.Infrastructure.Repositories.EFAuctionRepository(context)
                    .GetReservedQuantitiesAsync(null, cancellationToken);
                long reserved = reservations.GetValueOrDefault(item.LotId);
                if (item.Quantity <= 0 || stock.Quantity < reserved || stock.Quantity < item.Quantity)
                    throw new Auktionshuset.Application.Abstraction.Auction.AuctionStateConflictException(
                        $"Lagerkonflikt for {stock.Name} ({stock.LotId}). Afstem beholdning og reservationer før salg.");
                stock.Quantity -= item.Quantity;
            }

            item.OpenForBids = false;
            item.ClosedAtTime = nowUtc;
            item.CurrentHighestBidId = winner?.BidId;
            if (item.Auction.SelectedAuctionLotId == item.AuctionLotId)
                item.Auction.SelectedAuctionLotId = null;
            await AuctionRevision.AdvanceAsync(item.Auction, outboxWriter, nowUtc, cancellationToken);

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
