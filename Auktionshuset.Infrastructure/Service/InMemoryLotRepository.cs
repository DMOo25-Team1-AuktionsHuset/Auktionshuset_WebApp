using Auktionshuset.Application.Abstraction.Admin.Lots;
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
    }
}
