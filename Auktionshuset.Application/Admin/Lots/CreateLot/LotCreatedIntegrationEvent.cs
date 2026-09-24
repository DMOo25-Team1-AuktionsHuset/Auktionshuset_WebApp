using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Application.Admin.Lots.CreateLot
{
    public sealed record LotCreatedIntegrationEvent(
            Guid EventId,
            Guid LotId,
            Guid AuctionHouseId,
            string Name,
            string Category,
            int Quantity,
            decimal EstimatedValue,
            DateTime OccurredAt,
            string? ImageFileName = null) : IIntegrationEvent;
}

