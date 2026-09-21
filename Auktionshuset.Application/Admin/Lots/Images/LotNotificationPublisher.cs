using Auktionshuset.Application.Admin.Lots.UpdateLot;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Domain.Entities;

namespace Auktionshuset.Application.Admin.Lots.Images;

/// <summary>
/// Publishes an integration event describing a lot whose image changed, so connected clients can
/// refresh the list they are showing.
/// </summary>
internal static class LotNotificationPublisher
{
    internal static Task PublishUpdatedAsync(
        Lot lot,
        IIntegrationEventPublisher eventPublisher,
        CancellationToken cancellationToken) =>
        eventPublisher.PublishAsync(
            new LotUpdatedIntegrationEvent(
                EventId: Guid.NewGuid(),
                LotId: lot.LotId,
                AuctionHouseId: lot.AuctionHouseId,
                Name: lot.Name,
                Category: lot.Category,
                Quantity: lot.Quantity,
                EstimatedValue: lot.EstimatedValue,
                Description: lot.Description,
                Tags: lot.Tags,
                ImageFileName: lot.ImageFileName,
                OccurredAt: DateTime.Now),
            cancellationToken);
}
