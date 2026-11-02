using System.Reflection;
using Auktionshuset.Api.Services.DeviceGateway;

namespace Auktionshuset.Api.Test.Admins.Auctions;

public class DeviceGatewayProcessorTests
{
    [Fact]
    public async Task RoutesNotificationsOnlyToAffectedDevices()
    {
        var gateway = new DeviceGatewayProcessor();
        var auction = Guid.NewGuid();
        var first = new Device(Guid.NewGuid(), auction);
        var second = new Device(Guid.NewGuid(), auction);
        var other = new Device(Guid.NewGuid(), Guid.NewGuid());
        Assert.True(gateway.Register(first));
        Assert.True(gateway.Register(second));
        Assert.True(gateway.Register(other));
        Assert.False(gateway.Register(first));
        await gateway.AuctionChangedAsync(Guid.NewGuid(), auction, 1, default);
        await gateway.AssignmentChangedAsync(Guid.NewGuid(), first.DeviceId, Guid.NewGuid(), default);
        await WaitUntilAsync(() => first.Changes == 1 && first.Assignments == 1 && second.Changes == 1);
        Assert.Equal(0, other.Changes);
        Assert.False(gateway.Unregister(new Device(first.DeviceId, auction)));
        Assert.True(gateway.Unregister(first));
        gateway.Unregister(second);
        gateway.Unregister(other);
    }

    [Fact]
    public async Task FailedDeviceIsRemovedWithoutBlockingHealthyDevice()
    {
        var gateway = new DeviceGatewayProcessor();
        var broken = new Device(Guid.NewGuid(), Guid.NewGuid()) { Fail = true };
        var healthy = new Device(Guid.NewGuid(), Guid.NewGuid());
        gateway.Register(broken);
        gateway.Register(healthy);
        await gateway.ResynchronizeAsync(default);
        Assert.True(broken.Aborted);
        Assert.Equal(1, healthy.Resyncs);
        Assert.False(gateway.Unregister(broken));
        await gateway.SetUnavailableAsync(default);
        Assert.Equal(1, healthy.Unavailable);
        gateway.Unregister(healthy);
    }

    [Fact]
    public async Task DeviceUnregisteringDuringRefreshIsNotAborted()
    {
        var gateway = new DeviceGatewayProcessor();
        var device = new SelfUnregisteringDevice(gateway);
        Assert.True(gateway.Register(device));

        await gateway.ResynchronizeAsync(default);

        Assert.True(device.Unregistered);
        Assert.False(device.Aborted);
    }

    [Fact]
    public async Task SlowDeviceDoesNotBlockOtherDeviceNotificationsAndIsAbortedOnTimeout()
    {
        var gateway = new DeviceGatewayProcessor(TimeSpan.FromMilliseconds(300));
        var auction = Guid.NewGuid();
        var slow = new Device(Guid.NewGuid(), auction) { BlockChanges = true };
        var healthy = new Device(Guid.NewGuid(), auction);
        gateway.Register(slow);
        gateway.Register(healthy);
        await gateway.ResynchronizeAsync(default);
        SetConnected(gateway, true);
        await gateway.AuctionChangedAsync(Guid.NewGuid(), auction, 1, default);
        await slow.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(gateway.IsDeviceReady(slow));
        await gateway.AuctionChangedAsync(Guid.NewGuid(), auction, 2, default);
        await WaitUntilAsync(() => healthy.Changes == 2);
        Assert.True(gateway.IsConnected);
        await WaitUntilAsync(() => slow.Aborted);
        Assert.False(gateway.IsDeviceReady(slow));
        Assert.True(gateway.IsDeviceReady(healthy));
        slow.Release.TrySetResult();
        gateway.Unregister(healthy);
    }

    [Fact]
    public async Task ReconnectResynchronizesRegistrationAlreadyInProgressBeforeReady()
    {
        var gateway = new DeviceGatewayProcessor();
        var deviceId = Guid.NewGuid(); var sessionId = Guid.NewGuid();
        var reader = new BlockingReader(new(deviceId, sessionId, Guid.NewGuid(), Guid.NewGuid(), 1, null, null, null, null, null, null));
        var connection = new DeviceSnapshotConnection(deviceId, sessionId, gateway, reader, new Sink());
        var registration = connection.RegisterAsync(default);
        await reader.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var reconnect = (Task)typeof(DeviceGatewayProcessor).GetMethod("SynchronizeConnectionAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(gateway, [CancellationToken.None])!;
        Assert.False(gateway.IsConnected);
        Assert.False(reconnect.IsCompleted);
        reader.Release.TrySetResult();
        Assert.True(await registration);
        await reconnect.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(2, reader.Reads);
        Assert.False(gateway.IsDeviceReady(connection));
        SetConnected(gateway, true);
        Assert.True(gateway.IsDeviceReady(connection));
        connection.Unregister();
    }

    [Fact]
    public async Task RegistrationDuringReconnectHasItsOwnSnapshotAndStaysUnreadyUntilConnected()
    {
        var gateway = new DeviceGatewayProcessor();
        var existing = new Device(Guid.NewGuid(), Guid.NewGuid()) { BlockResync = true };
        gateway.Register(existing);
        var reconnect = (Task)typeof(DeviceGatewayProcessor).GetMethod("SynchronizeConnectionAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(gateway, [CancellationToken.None])!;
        await existing.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var deviceId = Guid.NewGuid(); var sessionId = Guid.NewGuid();
        var reader = new BlockingReader(new(deviceId, sessionId, Guid.NewGuid(), Guid.NewGuid(), 1, null, null, null, null, null, null));
        reader.Release.TrySetResult();
        var connection = new DeviceSnapshotConnection(deviceId, sessionId, gateway, reader, new Sink());
        Assert.True(await connection.RegisterAsync(default));
        Assert.False(gateway.IsConnected);
        Assert.False(gateway.IsDeviceReady(connection));
        existing.Release.TrySetResult();
        await reconnect.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(gateway.IsConnected);
        SetConnected(gateway, true);
        Assert.True(gateway.IsDeviceReady(connection));
        connection.Unregister();
        gateway.Unregister(existing);
    }

    [Fact]
    public async Task TimedOutSnapshotCannotPublishAfterReaderEventuallyReturns()
    {
        var gateway = new DeviceGatewayProcessor(TimeSpan.FromMilliseconds(100));
        var deviceId = Guid.NewGuid(); var sessionId = Guid.NewGuid();
        var reader = new BlockingReader(new(deviceId, sessionId, Guid.NewGuid(), Guid.NewGuid(), 1, null, null, null, null, null, null));
        var sink = new Sink();
        var connection = new DeviceSnapshotConnection(deviceId, sessionId, gateway, reader, sink);
        var registration = connection.RegisterAsync(default);
        await reader.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(await registration.WaitAsync(TimeSpan.FromSeconds(2)));
        reader.Release.TrySetResult();
        await Task.Delay(100);
        Assert.Equal(0, sink.Applied);
        Assert.False(gateway.IsDeviceReady(connection));
    }

    private static void SetConnected(DeviceGatewayProcessor gateway, bool value) =>
        typeof(DeviceGatewayProcessor).GetMethod("SetConnected", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(gateway, [value]);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!condition()) await Task.Delay(10, timeout.Token);
    }

    private sealed class Device(Guid id, Guid auctionId) : IConnectedGatewayDevice
    {
        private int changes, assignments, resyncs, unavailable, aborted;
        public Guid DeviceId => id;
        public Guid AuctionId => auctionId;
        public bool Fail { get; init; }
        public bool BlockChanges { get; init; }
        public bool BlockResync { get; init; }
        public int Changes => Volatile.Read(ref changes);
        public int Assignments => Volatile.Read(ref assignments);
        public int Resyncs => Volatile.Read(ref resyncs);
        public int Unavailable => Volatile.Read(ref unavailable);
        public bool Aborted => Volatile.Read(ref aborted) != 0;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Abort() => Interlocked.Exchange(ref aborted, 1);
        public Task SetUnavailableAsync(CancellationToken cancellationToken) { Interlocked.Increment(ref unavailable); return Result(); }
        public Task ResynchronizeAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref resyncs);
            if (!BlockResync) return Result();
            Started.TrySetResult();
            return Release.Task;
        }
        public Task AuctionChangedAsync(Guid eventId, Guid auctionId, long revision, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref changes);
            Started.TrySetResult();
            return BlockChanges ? Release.Task : Result();
        }
        public Task AssignmentChangedAsync(Guid eventId, Guid deviceId, Guid sessionId, CancellationToken cancellationToken) { Interlocked.Increment(ref assignments); return Result(); }
        private Task Result() => Fail ? Task.FromException(new InvalidOperationException()) : Task.CompletedTask;
    }

    private sealed class SelfUnregisteringDevice(DeviceGatewayProcessor gateway) : IConnectedGatewayDevice
    {
        public Guid DeviceId { get; } = Guid.NewGuid();
        public Guid AuctionId => Guid.Empty;
        public bool Unregistered { get; private set; }
        public bool Aborted { get; private set; }
        public void Abort() => Aborted = true;
        public Task SetUnavailableAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public async Task ResynchronizeAsync(CancellationToken cancellationToken)
        {
            Unregistered = gateway.Unregister(this);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        public Task AuctionChangedAsync(Guid eventId, Guid auctionId, long revision, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AssignmentChangedAsync(Guid eventId, Guid deviceId, Guid sessionId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class BlockingReader(DeviceCurrentState state) : IDeviceCurrentStateReader
    {
        private int reads;
        public int Reads => Volatile.Read(ref reads);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<DeviceCurrentState?> ReadAsync(Guid deviceId, Guid sessionId, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref reads) == 1) { Started.TrySetResult(); await Release.Task; }
            return state;
        }
    }

    private sealed class Sink : IDeviceSnapshotSink
    {
        private int applied;
        public int Applied => Volatile.Read(ref applied);
        public Task SetUnavailableAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ApplySnapshotAsync(DeviceCurrentState state, long connectionStamp, CancellationToken cancellationToken) { Interlocked.Increment(ref applied); return Task.CompletedTask; }
        public Task SetReleasedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
