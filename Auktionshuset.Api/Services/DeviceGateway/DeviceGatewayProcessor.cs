using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Auktionshuset.Api.Services.DeviceGateway;

public interface IConnectedGatewayDevice
{
    Guid DeviceId { get; }
    Guid AuctionId { get; }
    Task SetUnavailableAsync(CancellationToken cancellationToken);
    Task ResynchronizeAsync(CancellationToken cancellationToken);
    Task AuctionChangedAsync(Guid eventId, Guid auctionId, long auctionRevision, CancellationToken cancellationToken);
    Task AssignmentChangedAsync(Guid eventId, Guid deviceId, Guid sessionId, CancellationToken cancellationToken);
    void Invalidate() { }
    void Abort() { }
    Task NotifyReadyAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class DeviceGatewayProcessor
{
    private readonly ConcurrentDictionary<Guid, Entry> devices = new();
    private readonly object registrations = new();
    private readonly TimeSpan deviceTimeout;
    private long connectionStamp;
    private volatile bool isConnected;

    public DeviceGatewayProcessor() : this(TimeSpan.FromSeconds(5)) { }
    public DeviceGatewayProcessor(TimeSpan deviceTimeout)
    {
        if (deviceTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(deviceTimeout));
        this.deviceTimeout = deviceTimeout;
    }

    public long ConnectionStamp => Interlocked.Read(ref connectionStamp);
    public bool IsConnected => isConnected;
    internal void SetConnected(bool connected)
    {
        lock (registrations)
        {
            if (connected && (ConnectionStamp & 1) == 0 || !connected && (ConnectionStamp & 1) != 0)
                Interlocked.Increment(ref connectionStamp);
            isConnected = connected;
            if (connected)
                foreach (var entry in devices.Values) entry.NotifyReady();
        }
    }

    internal Task SynchronizeConnectionAsync(CancellationToken cancellationToken)
    {
        lock (registrations)
        {
            isConnected = false;
            if ((ConnectionStamp & 1) == 0) Interlocked.Increment(ref connectionStamp);
            return Task.WhenAll(devices.Values.Select(entry => WaitForDeviceAsync(entry,
                entry.Enqueue((device, ct) => device.ResynchronizeAsync(ct), cancellationToken), cancellationToken)));
        }
    }

    public bool IsDeviceReady(IConnectedGatewayDevice device) =>
        IsConnected && devices.TryGetValue(device.DeviceId, out var entry) &&
        ReferenceEquals(entry.Device, device) && entry.IsReady;

    public bool Register(IConnectedGatewayDevice device)
    {
        lock (registrations) return Add(device) is not null;
    }

    internal async Task<bool> RegisterAndSynchronizeAsync(IConnectedGatewayDevice device, CancellationToken cancellationToken)
    {
        Entry? entry;
        Task<bool> synchronized;
        lock (registrations)
        {
            entry = Add(device);
            if (entry is null) return false;
            synchronized = entry.Enqueue((d, ct) => d.ResynchronizeAsync(ct), cancellationToken);
        }
        return await synchronized && devices.TryGetValue(device.DeviceId, out var current) && ReferenceEquals(current, entry);
    }

    private Entry? Add(IConnectedGatewayDevice device)
    {
        var entry = new Entry(this, device, deviceTimeout);
        if (!devices.TryAdd(device.DeviceId, entry)) return null;
        entry.Start();
        return entry;
    }

    public bool Unregister(IConnectedGatewayDevice device)
    {
        lock (registrations)
        {
            if (!devices.TryGetValue(device.DeviceId, out var entry) || !ReferenceEquals(entry.Device, device) ||
                !devices.TryRemove(new KeyValuePair<Guid, Entry>(device.DeviceId, entry))) return false;
            entry.Stop();
            return true;
        }
    }

    public Task<bool> ResynchronizeDeviceAsync(IConnectedGatewayDevice device, CancellationToken cancellationToken) =>
        devices.TryGetValue(device.DeviceId, out var entry) && ReferenceEquals(entry.Device, device)
            ? entry.Enqueue((d, ct) => d.ResynchronizeAsync(ct), cancellationToken)
            : Task.FromResult(false);

    public Task SetUnavailableAsync(CancellationToken cancellationToken) =>
        ApplyAndWaitAsync((device, ct) => device.SetUnavailableAsync(ct), cancellationToken);

    public Task ResynchronizeAsync(CancellationToken cancellationToken) =>
        ApplyAndWaitAsync((device, ct) => device.ResynchronizeAsync(ct), cancellationToken);

    private Task ApplyAndWaitAsync(Func<IConnectedGatewayDevice, CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        Task[] tasks;
        // A registration is either in this snapshot or starts its own refresh after this point.
        lock (registrations)
            tasks = devices.Values.Select(entry => WaitForDeviceAsync(entry, entry.Enqueue(action, cancellationToken), cancellationToken)).ToArray();
        return Task.WhenAll(tasks);
    }

    private async Task WaitForDeviceAsync(Entry entry, Task<bool> work, CancellationToken cancellationToken)
    {
        try { await work.WaitAsync(deviceTimeout, cancellationToken); }
        catch (TimeoutException) { entry.Fail(); }
    }

    public Task AuctionChangedAsync(Guid eventId, Guid auctionId, long revision, CancellationToken cancellationToken)
    {
        foreach (var entry in devices.Values)
            if (entry.Device is DeviceSnapshotConnection || entry.Device.AuctionId == auctionId)
                _ = entry.Enqueue((device, ct) => device.AuctionChangedAsync(eventId, auctionId, revision, ct), cancellationToken);
        return Task.CompletedTask;
    }

    public Task AssignmentChangedAsync(Guid eventId, Guid deviceId, Guid sessionId, CancellationToken cancellationToken)
    {
        if (devices.TryGetValue(deviceId, out var entry))
            _ = entry.Enqueue((device, ct) => device.AssignmentChangedAsync(eventId, deviceId, sessionId, ct), cancellationToken);
        return Task.CompletedTask;
    }

    private sealed class Entry(DeviceGatewayProcessor owner, IConnectedGatewayDevice device, TimeSpan timeout)
    {
        private readonly Channel<Work> queue = Channel.CreateBounded<Work>(new BoundedChannelOptions(32)
        {
            SingleReader = true, FullMode = BoundedChannelFullMode.Wait
        });
        private readonly CancellationTokenSource lifetime = new();
        private int pending;
        private int stopped;
        private int synchronized;
        public IConnectedGatewayDevice Device => device;
        public bool IsReady => Volatile.Read(ref stopped) == 0 && Volatile.Read(ref pending) == 0 && Volatile.Read(ref synchronized) != 0;

        public void Start() => _ = Task.Run(RunAsync);

        public void NotifyReady() => _ = Task.Run(async () =>
        {
            if (!IsReady) return;
            try { await Task.Run(() => device.NotifyReadyAsync(CancellationToken.None)).WaitAsync(timeout); }
            catch { Fail(); }
        });

        public Task<bool> Enqueue(Func<IConnectedGatewayDevice, CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Interlocked.Increment(ref pending);
            try
            {
                device.Invalidate();
                if (Volatile.Read(ref stopped) == 0 && queue.Writer.TryWrite(new Work(action, cancellationToken, completion)))
                    return completion.Task;
            }
            catch { }
            Interlocked.Decrement(ref pending);
            Fail();
            completion.TrySetResult(false);
            return completion.Task;
        }

        public void Stop()
        {
            if (Interlocked.Exchange(ref stopped, 1) == 0)
            {
                queue.Writer.TryComplete();
                lifetime.Cancel();
            }
        }

        public void Fail()
        {
            Stop();
            try { device.Abort(); } catch { }
            owner.Unregister(device);
        }

        private async Task RunAsync()
        {
            await foreach (var work in queue.Reader.ReadAllAsync())
            {
                var success = false;
                if (Volatile.Read(ref stopped) == 0)
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(work.CancellationToken, lifetime.Token);
                    deadline.CancelAfter(timeout);
                    var operation = Task.Run(() => work.Action(device, deadline.Token));
                    try
                    {
                        await operation.WaitAsync(deadline.Token);
                        success = true;
                        Volatile.Write(ref synchronized, 1);
                    }
                    catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
                    {
                        _ = operation.ContinueWith(t => _ = t.Exception, CancellationToken.None,
                            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    }
                    catch
                    {
                        _ = operation.ContinueWith(t => _ = t.Exception, CancellationToken.None,
                            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                        Fail();
                    }
                }
                Interlocked.Decrement(ref pending);
                if (success && IsReady)
                {
                    try { await Task.Run(() => device.NotifyReadyAsync(CancellationToken.None)).WaitAsync(timeout); }
                    catch { success = false; Fail(); }
                }
                work.Completion.TrySetResult(success);
            }
        }

        private sealed record Work(Func<IConnectedGatewayDevice, CancellationToken, Task> Action,
            CancellationToken CancellationToken, TaskCompletionSource<bool> Completion);
    }
}
