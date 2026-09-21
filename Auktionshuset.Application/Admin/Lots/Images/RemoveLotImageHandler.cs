using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.Admin.Lots.UpdateLot;
using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Application.Admin.Lots.Images;

public sealed class RemoveLotImageHandler(
    ILotRepository lotRepository,
    ILotImageStore imageStore,
    IIntegrationEventPublisher eventPublisher)
{
    /// <summary>
    /// Removes the image of a lot. Removing a lot that has no image is not an error.
    /// </summary>
    /// <param name="lotId">The identifier of the lot whose image is removed.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The outcome of the removal.</returns>
    public async Task<LotImageResult> HandleAsync(Guid lotId, CancellationToken cancellationToken)
    {
        var lot = await lotRepository.GetByIdAsync(lotId, cancellationToken);

        if (lot is null)
        {
            return LotImageResult.Missing();
        }

        if (string.IsNullOrWhiteSpace(lot.ImageFileName))
        {
            return LotImageResult.Saved(lot.LotId, null);
        }

        var fileName = lot.ImageFileName;
        lot.ImageFileName = null;

        await lotRepository.UpdateAsync(lot, cancellationToken);
        await imageStore.DeleteAsync(fileName, cancellationToken);

        await LotNotificationPublisher.PublishUpdatedAsync(lot, eventPublisher, cancellationToken);

        return LotImageResult.Saved(lot.LotId, null);
    }
}
