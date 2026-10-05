using Auktionshuset.Application.Abstraction.Admin.Auctions;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Infrastructure.Repositories
{
    public sealed class EFAuctionRepository(AHDBContext context)
        : IAuctionRepository
    {

        public Task AddAsync(Auction auction,
            IReadOnlyCollection<AuctionLot> auctionLots,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            context.Entry(auction).State = EntityState.Added;

            foreach (AuctionLot selectedLots in auctionLots)
            {
                context.AuctionLot.Add(CreateRow(selectedLots));
            }
            
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<Auction>> GetAllAsync(CancellationToken cancellationToken)
        {
            return await context.Auction
                .AsNoTracking()
                .Include(auction => auction.Employee)
                .OrderByDescending(auction => auction.StartsAt)
                .ThenBy(auction => auction.Name)
                .ToListAsync(cancellationToken);
        }

        public Task<Auction?> GetByIdAsync(Guid auctionId, CancellationToken cancellationToken)
        {
            return context.Auction
                .Include(auction => auction.Employee)
                .SingleOrDefaultAsync(
                    auction => auction.AuctionId == auctionId,
                    cancellationToken
                );
        }

        public async Task<IReadOnlyList<AuctionLot>> GetAuctionLotsAsync(Guid auctionId, CancellationToken cancellationToken)
        {
            return await context.AuctionLot
                .AsNoTracking()
                .Include(auctionLot => auctionLot.Lot)
                .Where(auctionLot => auctionLot.AuctionId == auctionId)
                .OrderBy(auctionLot => auctionLot.Lot.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> UpdateAsync(Auction auction, IReadOnlyCollection<AuctionLot> auctionLots,
            CancellationToken cancellationToken)
        {
            Auction? storedAuction = await context.Auction.FindAsync([auction.AuctionId], cancellationToken);
            if (storedAuction == null) return false;
            

            // Update the auction properties
            context.Entry(storedAuction).CurrentValues.SetValues(auction);

            // Update the auction lots
            List<AuctionLot> existingLots = await context.AuctionLot
                .Where(lot => lot.AuctionId == auction.AuctionId)
                .ToListAsync(cancellationToken);

            foreach (AuctionLot lot in existingLots)
            {
                context.AuctionLot.Remove(lot);
            }

            foreach (AuctionLot lot in auctionLots)
            {
                context.AuctionLot.Add(CreateRow(lot));
            }

            return true;
        }

        public async Task<bool> DeleteAsync(Guid auctionId, CancellationToken cancellationToken)
        {
            Auction? storedAuction = await context.Auction.FindAsync([auctionId], cancellationToken);
            if (storedAuction == null) return false;

            context.Auction.Remove(storedAuction);
            return true;
        }

        private static AuctionLot CreateRow(AuctionLot auctionLot)
        {
            return new AuctionLot
            {
                AuctionId = auctionLot.AuctionId,
                AuctionLotId = auctionLot.AuctionLotId,
                LotId = auctionLot.LotId,
                Quantity = auctionLot.Quantity,
                StartingPrice = auctionLot.StartingPrice,
                OpenForBids = auctionLot.OpenForBids,
                CurrentHighestBidId = auctionLot.CurrentHighestBidId
            };
        }
    }
}
