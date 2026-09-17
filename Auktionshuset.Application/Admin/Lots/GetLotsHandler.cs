using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Domain.Entities;

namespace Auktionshuset.Application.Admin.Lots;

public sealed class GetLotsHandler(ILotRepository lotRepository)
{
    public Task<IReadOnlyList<Lot>> HandleAsync(CancellationToken cancellationToken) =>
        lotRepository.GetAllAsync(cancellationToken);
}
