using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.Admin.Lots.UpdateLot;
using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Application.Admin.Lots.Images;

public sealed class UploadLotImageHandler(
    ILotRepository lotRepository,
    ILotImageStore imageStore,
    IIntegrationEventPublisher eventPublisher)
{
    /// <summary>
    /// Validates and stores an uploaded image for a lot, replacing any image it already had.
    /// </summary>
    /// <param name="command">The upload together with the lot it belongs to.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The outcome of the upload.</returns>
    public async Task<LotImageResult> HandleAsync(
        UploadLotImageCommand command,
        CancellationToken cancellationToken)
    {
        var lot = await lotRepository.GetByIdAsync(command.LotId, cancellationToken);

        if (lot is null)
        {
            return LotImageResult.Missing();
        }

        if (command.Length <= 0)
        {
            return LotImageResult.Invalid(["Vælg en billedfil, der skal vedhæftes genstanden."]);
        }

        if (command.Length > LotImageValidator.MaxSizeInBytes)
        {
            return LotImageResult.Invalid(["Billedet må højst være 5 MB."]);
        }

        if (!LotImageValidator.TryValidate(command.Content, command.Length, out var extension))
        {
            return LotImageResult.Invalid(["Billedet skal være i formatet JPEG, PNG eller WebP."]);
        }

        var previousFileName = lot.ImageFileName;
        var fileName = await imageStore.SaveAsync(command.Content, extension, cancellationToken);

        lot.ImageFileName = fileName;
        await lotRepository.UpdateAsync(lot, cancellationToken);

        if (!string.IsNullOrWhiteSpace(previousFileName))
        {
            await imageStore.DeleteAsync(previousFileName, cancellationToken);
        }

        await LotNotificationPublisher.PublishUpdatedAsync(lot, eventPublisher, cancellationToken);

        return LotImageResult.Saved(lot.LotId, fileName);
    }
}
