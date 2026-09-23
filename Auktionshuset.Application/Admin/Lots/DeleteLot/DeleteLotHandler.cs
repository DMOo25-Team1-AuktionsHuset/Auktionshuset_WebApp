using System.Reflection;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Application.Admin.Lots.DeleteLot;

public sealed class DeleteLotHandler(
    ILotRepository lotRepository, 
    IIntegrationEventPublisher eventPublisher)
{
    /// <summary>
    /// Deletes the lot referenced by the specified command.
    /// </summary>
    /// <param name="command">The command identifying the lot to delete.</param>
    /// <returns><see langword="true"/> if the lot was found and deleted; otherwise, <see langword="false"/>.</returns>
    
    public async Task<bool> HandleAsync(
        DeleteLotCommand command, 
        CancellationToken cancellationToken)
    {
        var deleted = await lotRepository.DeleteAsync(
            command.LotId, 
            cancellationToken);

        if (!deleted)
        {
            return false;
        }

        LotDeletedIntegrationEvent integrationEvent = new LotDeletedIntegrationEvent(
            EventId: Guid.NewGuid(),
            LotId: command.LotId,
            OccurredAt: DateTime.UtcNow);

        await eventPublisher.PublishAsync(
            integrationEvent,
            cancellationToken);

        return true;
    }
}
