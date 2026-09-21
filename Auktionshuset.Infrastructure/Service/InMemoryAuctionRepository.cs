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

        /// <summary>
        /// Returns every stored auction, ordered by start time with the most recent first.
        /// </summary>
        /// <returns>A read-only snapshot of all auctions.</returns>
        public Task<IReadOnlyList<Auction>> GetAllAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<Auction> auctions = _auctions.Values
                .OrderByDescending(auction => auction.StartsAt)
                .ThenBy(auction => auction.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            return Task.FromResult(auctions);
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

        /// <summary>
        /// Replaces a stored auction and its lot relationships.
        /// </summary>
        /// <param name="auction">The auction holding the updated values.</param>
        /// <param name="auctionLots">The lot relationships that replace the stored ones.</param>
        /// <returns><see langword="true"/> when the auction existed and was updated; otherwise, <see langword="false"/>.</returns>
        public Task<bool> UpdateAsync(
            Auction auction,
            IReadOnlyCollection<AuctionLot> auctionLots,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_auctions.ContainsKey(auction.AuctionId))
            {
                return Task.FromResult(false);
            }

            _auctions[auction.AuctionId] = auction;
            _auctionLots[auction.AuctionId] = auctionLots.ToArray();

            return Task.FromResult(true);
        }

        /// <summary>
        /// Removes an auction together with its lot relationships.
        /// </summary>
        /// <param name="auctionId">The identifier of the auction to remove.</param>
        /// <returns><see langword="true"/> when the auction existed and was removed; otherwise, <see langword="false"/>.</returns>
        public Task<bool> DeleteAsync(Guid auctionId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var removed = _auctions.TryRemove(auctionId, out _);
            _auctionLots.TryRemove(auctionId, out _);

            return Task.FromResult(removed);
        }
    }
}
