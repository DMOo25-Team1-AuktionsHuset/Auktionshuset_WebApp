namespace Auktionshuset.Api.Services.DeviceGateway;


public interface IDeviceSnapshotSink
{
    Task SetUnavailableAsync(CancellationToken cancellationToken);
    Task ApplySnapshotAsync(DeviceCurrentState state, long connectionStamp, CancellationToken cancellationToken);
    Task SetReleasedAsync(CancellationToken cancellationToken);
    void Invalidate() { }
    void Abort() { }
    Task NotifyReadyAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class DeviceSnapshotConnection : IConnectedGatewayDevice
{
    private readonly DeviceGatewayProcessor gateway;
    private readonly IDeviceCurrentStateReader reader;
    private readonly IDeviceSnapshotSink sink;
    private readonly SemaphoreSlim gate = new(1, 1);
    private DeviceCurrentState? current;
    private bool released;

    public DeviceSnapshotConnection(Guid deviceId, Guid sessionId, DeviceGatewayProcessor gateway,
        IDeviceCurrentStateReader reader, IDeviceSnapshotSink sink)
    {
        DeviceId = deviceId;
        SessionId = sessionId;
        this.gateway = gateway;
        this.reader = reader;
        this.sink = sink;
    }

    public Guid DeviceId { get; }
    public Guid SessionId { get; }
    public Guid AuctionId => current?.AuctionId ?? Guid.Empty;

    public async Task<bool> RegisterAsync(CancellationToken cancellationToken)
    {
        await sink.SetUnavailableAsync(cancellationToken);
        try { return await gateway.RegisterAndSynchronizeAsync(this, cancellationToken) && !released; }
        catch { gateway.Unregister(this); throw; }
    }

    public bool Unregister() => gateway.Unregister(this);
    public void Invalidate() => sink.Invalidate();
    public void Abort() => sink.Abort();
    public Task NotifyReadyAsync(CancellationToken cancellationToken) => sink.NotifyReadyAsync(cancellationToken);

    public async Task SetUnavailableAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try { if (!released) await sink.SetUnavailableAsync(cancellationToken); }
        finally { gate.Release(); }
    }

    public async Task ResynchronizeAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try { await RefreshAsync(cancellationToken); }
        finally { gate.Release(); }
    }

    public async Task AuctionChangedAsync(Guid eventId, Guid auctionId, long auctionRevision, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!released && (current is null || current.AuctionId == auctionId && auctionRevision > current.AuctionRevision))
                await RefreshAsync(cancellationToken, auctionId, auctionRevision);
        }
        finally { gate.Release(); }
    }

    public Task AssignmentChangedAsync(Guid eventId, Guid deviceId, Guid sessionId, CancellationToken cancellationToken) =>
        ResynchronizeAsync(cancellationToken);

    private async Task RefreshAsync(CancellationToken cancellationToken, Guid? notifiedAuction = null, long notifiedRevision = 0)
    {
        if (released) return;
        await sink.SetUnavailableAsync(cancellationToken);
        var connectionStamp = gateway.ConnectionStamp;
        var state = await reader.ReadAsync(DeviceId, SessionId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (state is null)
        {
            current = null;
            await sink.SetReleasedAsync(cancellationToken);
            released = true;
            gateway.Unregister(this);
            return;
        }
        if (state.DeviceId != DeviceId || state.SessionId != SessionId)
            throw new InvalidOperationException("The device snapshot does not match the registered session.");
        if (current is not null && (state.AuctionId != current.AuctionId || state.SessionRevision != current.SessionRevision || state.AuctionRevision < current.AuctionRevision))
            throw new InvalidOperationException("The device snapshot is older than or inconsistent with the current session state.");
        if (state.AuctionId == notifiedAuction && state.AuctionRevision < notifiedRevision)
            throw new InvalidOperationException("The device snapshot does not include the notified auction revision.");
        await sink.ApplySnapshotAsync(state, connectionStamp, cancellationToken);
        current = state;
    }
}
