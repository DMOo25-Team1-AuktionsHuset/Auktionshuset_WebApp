using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Api.Test;

/// <summary>
/// Records every integration event a handler publishes, without touching the message bus.
/// </summary>
public sealed class RecordingEventPublisher : IIntegrationEventPublisher
{
    public List<IIntegrationEvent> Published { get; } = [];

    /// <summary>
    /// Gets every published event of the requested type.
    /// </summary>
    public IEnumerable<TEvent> OfType<TEvent>() where TEvent : IIntegrationEvent =>
        Published.OfType<TEvent>();

    public Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        Published.Add(message);
        return Task.CompletedTask;
    }
}
