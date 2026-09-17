using Auktionshuset.Application.Abstraction.Admin.Lots;

namespace Auktionshuset.Application.Admin.Lots.DeleteLot;

public sealed class DeleteLotHandler(ILotRepository lotRepository)
{
    /// <summary>
    /// Deletes the lot referenced by the specified command.
    /// </summary>
    /// <param name="command">The command identifying the lot to delete.</param>
    /// <returns><see langword="true"/> if the lot was found and deleted; otherwise, <see langword="false"/>.</returns>
    public Task<bool> HandleAsync(DeleteLotCommand command, CancellationToken cancellationToken) =>
        lotRepository.DeleteAsync(command.LotId, cancellationToken);
}
