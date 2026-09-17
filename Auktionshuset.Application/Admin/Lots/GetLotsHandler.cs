using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Domain.Entities;

namespace Auktionshuset.Application.Admin.Lots;

public sealed class GetLotsHandler(ILotRepository lotRepository)
{
    /// <summary>
    /// Gets all lots.
    /// </summary>
    /// <returns>A read-only list of every stored lot.</returns>
    public Task<IReadOnlyList<Lot>> HandleAsync(CancellationToken cancellationToken) =>
        lotRepository.GetAllAsync(cancellationToken);
}
