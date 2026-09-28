namespace Auktionshuset.Application.EventHandling
{
    public interface IIntegrationEvent
    {
        Guid EventId { get; }
        DateTime OccurredAt { get; }
    }
}
