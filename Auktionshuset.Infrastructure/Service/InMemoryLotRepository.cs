using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Domain.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Infrastructure.Service {
    public class InMemoryLotRepository : ILotRepository {
        private readonly ConcurrentDictionary<Guid, Domain.Entities.Lot> _lots = [];

        public Task AddAsync(Domain.Entities.Lot lot, CancellationToken cancellationToken) {
            if(!_lots.TryAdd(lot.LotId, lot)) {
                throw new InvalidOperationException($"A lot with ID {lot.LotId} already exists");
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Domain.Entities.Lot>> GetAllAsync(CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<Domain.Entities.Lot> lots = _lots.Values
                .OrderBy(lot => lot.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            return Task.FromResult(lots);
        }

        public Task<Lot?> GetByIdAsync(Guid lotId, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();

            _lots.TryGetValue(lotId, out var lot);

            return Task.FromResult(lot);
        }

        public Task UpdateAsync(Lot lot, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_lots.ContainsKey(lot.LotId)) {
                throw new KeyNotFoundException($"Lot {lot.LotId} was not found");
            }

            _lots[lot.LotId] = lot;

            return Task.CompletedTask;
        }
    }
}
