using Auktionshuset.Application.EventHandling;
using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.Abstraction.Auction;

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

/// <summary>
/// Provides sessions for in-memory endpoint tests; it does not simulate database locks or rollback.
/// </summary>
internal sealed class RecordingAuctionWriteSessionFactory(IUnitOfWork unitOfWork)
    : IAuctionWriteSessionFactory
{
    public Task<IAuctionWriteSession> BeginAsync(
        Guid auctionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IAuctionWriteSession>(new Session(unitOfWork));
    }

    private sealed class Session(IUnitOfWork unitOfWork) : IAuctionWriteSession
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            unitOfWork.CommitBatchAsync(cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
