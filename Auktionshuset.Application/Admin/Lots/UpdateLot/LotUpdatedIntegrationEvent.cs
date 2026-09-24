using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Application.Admin.Lots.UpdateLot
{
    public sealed record LotUpdatedIntegrationEvent(
        Guid EventId,
        Guid LotId,
        Guid AuctionHouseId,
        string Name,
        string Category,
        int Quantity,
        decimal EstimatedValue,
        string Description,
        IReadOnlyCollection<string> Tags,
        DateTime OccurredAt,
        string? ImageFileName = null) : IIntegrationEvent;
}
