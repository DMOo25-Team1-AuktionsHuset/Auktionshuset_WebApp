using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Application.Admin.Lots.DeleteLot
{
    public sealed record LotDeletedIntegrationEvent(
        Guid EventId,
        Guid LotId,
        DateTime OccurredAt) : IIntegrationEvent;
}
