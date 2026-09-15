using Auktionshuset.Application.Abstraction.Admin.Lots;

namespace Auktionshuset.Application.Admin.Lots.DeleteLot;

public sealed class DeleteLotHandler(ILotRepository lotRepository)
{
    public Task<bool> HandleAsync(DeleteLotCommand command, CancellationToken cancellationToken) =>
        lotRepository.DeleteAsync(command.LotId, cancellationToken);
}
