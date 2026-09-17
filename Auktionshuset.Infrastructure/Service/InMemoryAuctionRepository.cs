using Auktionshuset.Application.Abstraction.Admin.Auctions;
using Auktionshuset.Domain.Entities;
using System.Collections.Concurrent;

namespace Auktionshuset.Infrastructure.Service
{
    public class InMemoryAuctionRepository : IAuctionRepository
    {
        private readonly ConcurrentDictionary<Guid, Auction> _auctions = [];
        private readonly ConcurrentDictionary<Guid, IReadOnlyList<AuctionLot>> _auctionLots = [];

        public Task AddAsync(
            Auction auction,
            IReadOnlyCollection<AuctionLot> auctionLots,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_auctions.TryAdd(auction.AuctionId, auction))
            {
                throw new InvalidOperationException($"An auction with ID {auction.AuctionId} already exists");
            }

            _auctionLots[auction.AuctionId] = auctionLots.ToArray();

            return Task.CompletedTask;
        }

        public Task<Auction?> GetByIdAsync(Guid auctionId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _auctions.TryGetValue(auctionId, out var auction);

            return Task.FromResult(auction);
        }

        public Task<IReadOnlyList<AuctionLot>> GetAuctionLotsAsync(
            Guid auctionId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_auctionLots.TryGetValue(auctionId, out var auctionLots))
            {
                return Task.FromResult(auctionLots);
            }

            return Task.FromResult<IReadOnlyList<AuctionLot>>([]);
        }
    }
}
