using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Domain.Entities;
using System.Collections.Concurrent;

namespace Auktionshuset.Infrastructure.Service
{
    public class InMemoryLotRepository : ILotRepository
    {
        private readonly ConcurrentDictionary<Guid, Domain.Entities.Lot> _lots = [];

        /// <summary>
        /// Removes the lot with the given identifier from the in-memory store.
        /// </summary>
        /// <param name="lotId">The identifier of the lot to remove.</param>
        /// <returns><see langword="true"/> if a lot was removed; otherwise, <see langword="false"/>.</returns>
        public Task<bool> DeleteAsync(Guid lotId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_lots.TryRemove(lotId, out _));
        }

        /// <summary>
        /// Stores a new lot in the in-memory store.
        /// </summary>
        /// <param name="lot">The lot to store, keyed by its <see cref="Lot.LotId"/>.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a lot with the same <see cref="Lot.LotId"/> is already stored.
        /// </exception>
        public Task AddAsync(Domain.Entities.Lot lot, CancellationToken cancellationToken)
        {
            if (!_lots.TryAdd(lot.LotId, lot))
            {
                throw new InvalidOperationException($"A lot with ID {lot.LotId} already exists");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Returns a snapshot of all stored lots ordered by name.
        /// </summary>
        /// <returns>A read-only list containing every stored lot, ordered case-insensitively by name.</returns>
        public Task<IReadOnlyList<Domain.Entities.Lot>> GetAllAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<Domain.Entities.Lot> lots = _lots.Values
                .OrderBy(lot => lot.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            return Task.FromResult(lots);
        }

        /// <summary>
        /// Looks up a single lot by identifier.
        /// </summary>
        /// <param name="lotId">The identifier of the lot to look up.</param>
        /// <returns>The stored lot, or <see langword="null"/> when it is not present.</returns>
        public Task<Lot?> GetByIdAsync(Guid lotId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _lots.TryGetValue(lotId, out Lot? lot);

            return Task.FromResult(lot);
        }

        /// <summary>
        /// Overwrites a stored lot with the supplied values.
        /// </summary>
        /// <param name="lot">The lot whose values replace the stored entry with the same <see cref="Lot.LotId"/>.</param>
        /// <exception cref="KeyNotFoundException">
        /// Thrown when no stored lot matches <see cref="Lot.LotId"/>.
        /// </exception>
        public Task UpdateAsync(Lot lot, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_lots.ContainsKey(lot.LotId))
            {
                throw new KeyNotFoundException($"Lot {lot.LotId} was not found");
            }

            _lots[lot.LotId] = lot;

            return Task.CompletedTask;
        }
    }
}

