using Auktionshuset.Application.Abstraction.Auction;
using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Infrastructure.Service.Auctions
{
    public class AuctionLifeCycleStore(AHDBContext db, TimeProvider clock) : IAuctionLifeCycleStore
    {
        public async Task<bool> StartAsync(Guid auctionId, CancellationToken cancellationToken)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var auction = await db.Auction
                .Include(a => a.AuctionLots)
                .SingleOrDefaultAsync(a => a.AuctionId == auctionId, cancellationToken);

            if (auction == null)
            {
                return false;
            }

            auction.AuctionStatus = AuctionStatuses.Live;

            foreach (var auctionLot in auction.AuctionLots)
            {
                auctionLot.OpenForBids = true;
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return true;
        }

        public async Task<bool> CloseAsync(Guid auctionId, CancellationToken cancellationToken)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var auction = await db.Auction
                .Include(a => a.AuctionLots)
                .SingleOrDefaultAsync(
                    a => a.AuctionId == auctionId,
                    cancellationToken);

            if (auction is null)
            {
                return false;
            }

            foreach (var auctionLot in auction.AuctionLots)
            {
                auctionLot.OpenForBids = false;
            }

            auction.AuctionStatus = AuctionStatuses.Ended;
            auction.EndedAt = clock.GetUtcNow().UtcDateTime;

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return true;

        }
    }
}
