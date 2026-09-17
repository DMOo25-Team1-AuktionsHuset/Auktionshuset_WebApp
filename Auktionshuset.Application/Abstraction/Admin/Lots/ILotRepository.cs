using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Domain.Entities;

namespace Auktionshuset.Application.Abstraction.Admin.Lots {
    public interface ILotRepository {
        /// <summary>
        /// Adds a lot to the repository.
        /// </summary>
        /// <param name="lot">The lot to add.</param>
        Task AddAsync(Lot lot, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes the lot with the specified identifier.
        /// </summary>
        /// <param name="lotId">The identifier of the lot to delete.</param>
        /// <returns><see langword="true"/> if the lot was deleted; otherwise, <see langword="false"/>.</returns>
        Task<bool> DeleteAsync(Guid lotId, CancellationToken cancellationToken);

        /// <summary>
        /// Gets all lots, ordered by <see cref="Lot.Name"/>.
        /// </summary>
        /// <returns>A read-only list of every stored lot.</returns>
        Task<IReadOnlyList<Lot>> GetAllAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gets the lot with the specified identifier.
        /// </summary>
        /// <param name="lotId">The identifier of the lot to retrieve.</param>
        /// <returns>The matching lot, or <see langword="null"/> if no lot has the specified identifier.</returns>
        Task<Lot?> GetByIdAsync(Guid lotId, CancellationToken cancellationToken);

        /// <summary>
        /// Updates an existing lot with the values of the specified lot.
        /// </summary>
        /// <param name="lot">The lot holding the updated values.</param>
        Task UpdateAsync(Lot lot, CancellationToken cancellationToken);
    }
}

