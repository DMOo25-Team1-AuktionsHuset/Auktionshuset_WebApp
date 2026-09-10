using Auktionshuset.Domain.Entities;

namespace Auktionshuset.Application.Abstraction.Admin.Lots;

public sealed class GetLotsHandler(ILotRepository lotRepository)
{
    public Task<IReadOnlyList<Lot>> HandleAsync(CancellationToken cancellationToken) =>
        lotRepository.GetAllAsync(cancellationToken);
}
