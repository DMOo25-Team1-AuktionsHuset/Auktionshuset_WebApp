using Auktionshuset.Application.EventHandling;
using Auktionshuset.Application.Abstraction;

namespace Auktionshuset.Api.Test;

/// <summary>
/// Records every integration event a handler publishes, without touching the message bus.
/// </summary>
public sealed class RecordingEventPublisher : IIntegrationEventPublisher, IOutboxWriter
{
    public List<IIntegrationEvent> Published { get; } = [];

    public Task AddAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Published.Add(integrationEvent);
        return Task.CompletedTask;
    }

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

public sealed class RecordingUnitOfWork : IUnitOfWork
{
    public int CommitCount { get; private set; }

    public Task CommitBatchAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommitCount++;
        return Task.CompletedTask;
    }
}
